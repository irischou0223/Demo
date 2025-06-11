using Demo.Data.Entities;
using FirebaseAdmin.Messaging;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// Web 推播策略，與 App 共用 FCM 服務
    /// 流程說明：
    /// 1. 查產品 FCM 設定
    /// 2. 彙整本批次有效 token（外層已分批，每批至多 500）
    /// 3. 送出 MulticastMessage
    /// 4. 記錄發送與失敗 log
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

        /// <summary>
        /// 發送 Web 推播（外層已分批，每批 devices 同產品、數量已控管）
        /// </summary>
        public async Task SendAsync(List<DeviceInfo> devices, string title, string body, Dictionary<string, string> data, NotificationMsgTemplate template)
        {
            _logger.LogInformation("WebNotificationStrategy.SendAsync 開始, ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}",
            devices.FirstOrDefault()?.ProductInfoId, devices.Count);

            if (devices == null || devices.Count == 0)
            {
                _logger.LogWarning("WebNotificationStrategy.SendAsync 結束，裝置數量為0");
                return;
            }

            // 1. 找到產品對應的 FCM 設定
            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationConfigAsync(productInfoId);
            if (config == null)
            {
                _logger.LogError("WebNotificationStrategy: 查無 FCM 設定, ProductInfoId={ProductInfoId}", productInfoId);
                _logger.LogWarning("WebNotificationStrategy.SendAsync 結束，查無 FCM 設定");
                return;
            }

            // 2. 彙整本批次 FCM token
            var tokens = devices.Select(d => d.FcmToken).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            if (!tokens.Any())
            {
                _logger.LogWarning("WebNotificationStrategy: 無 FCM token, ProductInfoId={ProductInfoId}", productInfoId);
                _logger.LogWarning("WebNotificationStrategy.SendAsync 結束，無有效 FCM token");
                return;
            }

            // 3. 分批發送（FCM 一次最多 500 個 token）
            var app = FirebaseAppManager.GetOrCreateApp(productInfoId, config.FcmKey);
            var messaging = FirebaseMessaging.GetMessaging(app);

            var msg = new MulticastMessage
            {
                Tokens = tokens,
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
                tokens.Count, productInfoId, response.SuccessCount, response.FailureCount);

            if (response.FailureCount > 0)
            {
                for (int j = 0; j < response.Responses.Count; j++)
                {
                    if (!response.Responses[j].IsSuccess)
                    {
                        _logger.LogWarning("WebNotificationStrategy: Failed token {Token} for ProductInfoId={ProductInfoId}, error: {Error}",
                            tokens[j], productInfoId, response.Responses[j].Exception?.Message);
                    }
                }
            }
            _logger.LogInformation("WebNotificationStrategy.SendAsync 結束, ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}", productInfoId, devices.Count);
        }
    }
}
