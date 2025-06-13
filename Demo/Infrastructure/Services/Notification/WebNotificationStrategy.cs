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
            _logger.LogInformation("Web notification send started. ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}",
                devices.FirstOrDefault()?.ProductInfoId, devices.Count);

            if (devices == null || devices.Count == 0)
            {
                _logger.LogWarning("Web notification send aborted: no devices.");
                return;
            }

            // 1. 找到產品對應的 FCM 設定
            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationActionConfigAsync(productInfoId);
            if (config == null)
            {
                _logger.LogError("Web notification failed: FCM config not found. ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            // 2. 彙整本批次 FCM token
            var tokens = devices.Select(d => d.FcmToken).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            if (!tokens.Any())
            {
                _logger.LogWarning("Web notification send aborted: no valid FCM token. ProductInfoId={ProductInfoId}", productInfoId);
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
            _logger.LogInformation("Web notifications sent. ProductInfoId={ProductInfoId}, TokenCount={TokenCount}, Success={Success}, Failure={Failure}",
                productInfoId, tokens.Count, response.SuccessCount, response.FailureCount);

            if (response.FailureCount > 0)
            {
                for (int j = 0; j < response.Responses.Count; j++)
                {
                    if (!response.Responses[j].IsSuccess)
                    {
                        _logger.LogWarning("Web notification failed for token. ProductInfoId={ProductInfoId}, Token={Token}, Error={Error}",
                            productInfoId, tokens[j], response.Responses[j].Exception?.Message);
                    }
                }
            }
            _logger.LogInformation("Web notification send finished. ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}", productInfoId, devices.Count);
        }
    }
}
