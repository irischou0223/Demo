using Demo.Data;
using Demo.Data.Entities;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// Web 推播策略，與App共用FCM服務
    /// </summary>
    public class WebNotificationStrategy : INotificationStrategy
    {
        private readonly ILogger<WebNotificationStrategy> _logger;
        private readonly ConfigCacheService _configCache;

        public WebNotificationStrategy(ILogger<WebNotificationStrategy> logger, ConfigCacheService configCache)
        {
            _logger = logger;
            _configCache = configCache;
        }

        public async Task SendAsync(
            List<DeviceInfo> devices,
            string title,
            string body,
            Dictionary<string, string> data,
            NotificationMsgTemplate template)
        {
            if (devices == null || devices.Count == 0) return;

            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationConfigAsync(productInfoId);
            if (config == null)
            {
                _logger.LogError("WebNotificationStrategy: 查無 NotificationActionConfig, ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            var tokens = devices.Select(d => d.FcmToken).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            if (!tokens.Any())
            {
                _logger.LogWarning("WebNotificationStrategy: No FCM tokens to send for ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            var app = FirebaseAppManager.GetOrCreateApp(productInfoId, config.FcmKey);
            var messaging = FirebaseMessaging.GetMessaging(app);

            const int batchSize = 500;
            for (int i = 0; i < tokens.Count; i += batchSize)
            {
                var batchTokens = tokens.Skip(i).Take(batchSize).ToList();
                var msg = new MulticastMessage
                {
                    Tokens = batchTokens,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = data ?? new Dictionary<string, string>(),
                    Webpush = new WebpushConfig
                    {
                        Notification = new WebpushNotification
                        {
                            Icon = template?.Icon
                        },
                        FcmOptions = new WebpushFcmOptions
                        {
                            Link = template?.ClickActionWeb
                        }
                    }
                };
                var response = await messaging.SendEachForMulticastAsync(msg);
                _logger.LogInformation("WebNotificationStrategy: Sent {Count} tokens for ProductInfoId={ProductInfoId}, Success={Success}, Failure={Failure}",
                    batchTokens.Count, productInfoId, response.SuccessCount, response.FailureCount);
                if (response.FailureCount > 0)
                {
                    for (int j = 0; j < response.Responses.Count; j++)
                    {
                        if (!response.Responses[j].IsSuccess)
                        {
                            _logger.LogWarning("WebNotificationStrategy: Failed token {Token} for ProductInfoId={ProductInfoId}, error: {Error}",
                                batchTokens[j], productInfoId, response.Responses[j].Exception?.Message);
                        }
                    }
                }
            }
        }
    }
}
