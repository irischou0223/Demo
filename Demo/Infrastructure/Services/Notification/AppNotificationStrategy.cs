using Demo.Data.Entities;
using FirebaseAdmin.Messaging;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// APP 推播策略，透過 Firebase Admin SDK 實作
    /// 流程說明：
    /// 1. 查詢對應產品的 FCM 設定
    /// 2. 準備所有裝置 FCM token
    /// 3. 構建 MulticastMessage
    /// 4. 呼叫 Firebase 發送推播
    /// 5. 記錄失敗 token
    /// </summary>
    public class AppNotificationStrategy : INotificationStrategy
    {
        private readonly ILogger<AppNotificationStrategy> _logger;
        private readonly ConfigCacheService _configCache;

        public AppNotificationStrategy(ILogger<AppNotificationStrategy> logger, ConfigCacheService configCache)
        {
            _logger = logger;
            _configCache = configCache;
        }

        /// <summary>
        /// 發送 APP 推播（每次僅針對同一產品一批裝置。外層已分批）
        /// </summary>
        public async Task SendAsync(
            List<DeviceInfo> devices,
            string title,
            string body,
            Dictionary<string, string> data,
            NotificationMsgTemplate template)
        {
            if (devices == null || devices.Count == 0)
            {
                _logger.LogWarning("App notification send aborted: no devices specified.");
                return;
            }

            // 1. 找到對應產品的 FCM 設定
            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationActionConfigAsync(productInfoId);
            if (config == null)
            {
                _logger.LogError("App notification failed: FCM config not found. ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            // 2. 準備所有裝置 FCM token
            var tokens = devices.Select(d => d.FcmToken).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            if (!tokens.Any())
            {
                _logger.LogWarning("App notification send aborted: no valid FCM token. ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            // 3. 準備 MulticastMessage
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
                Android = new AndroidConfig
                {
                    Notification = new AndroidNotification
                    {
                        Sound = template?.Sound,
                        ClickAction = template?.ClickActionApp
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = template?.Sound,
                        Badge = int.TryParse(template?.Badge, out var badge) ? badge : (int?)null
                    }
                }
            };

            // 4. 發送訊息給所有 token
            var response = await messaging.SendEachForMulticastAsync(msg);

            _logger.LogInformation("App notifications sent. ProductInfoId={ProductInfoId}, TokenCount={TokenCount}, Success={Success}, Failure={Failure}",
                productInfoId, tokens.Count, response.SuccessCount, response.FailureCount);

            // 5. 記錄失敗 token
            if (response.FailureCount > 0)
            {
                for (int j = 0; j < response.Responses.Count; j++)
                {
                    if (!response.Responses[j].IsSuccess)
                    {
                        _logger.LogWarning("App notification failed for token. ProductInfoId={ProductInfoId}, Token={Token}, Error={Error}",
                            productInfoId, tokens[j], response.Responses[j].Exception?.Message);
                    }
                }
            }
        }
    }
}