using Demo.Data.Entities;
using Newtonsoft.Json;
using System.Text;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// LINE 推播策略，使用LINE官方帳號發送通知
    /// 流程說明：
    /// 1. 查產品LINE設定
    /// 2. 彙整Line ID
    /// 3. 逐一發送
    /// </summary>
    public class LineNotificationStrategy : INotificationStrategy
    {
        private readonly ConfigCacheService _configCache;
        private readonly ILogger<LineNotificationStrategy> _logger;

        public LineNotificationStrategy(
            ConfigCacheService configCache,
            ILogger<LineNotificationStrategy> logger)
        {
            _configCache = configCache;
            _logger = logger;
        }

        /// <summary>
        /// 發送 LINE 推播（傳入一批裝置，全部同一產品）
        /// </summary>
        public async Task SendAsync(List<DeviceInfo> devices, string title, string body, Dictionary<string, string> data, NotificationMsgTemplate template)
        {
            _logger.LogInformation("LINE notification send started. ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}", devices.FirstOrDefault()?.ProductInfoId, devices.Count);

            if (devices == null || devices.Count == 0)
            {
                _logger.LogWarning("LINE notification send aborted: no devices.");
                return;
            }

            // 1. 查產品對應 LINE 設定
            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationActionConfigAsync(productInfoId);
            if (config == null || string.IsNullOrWhiteSpace(config.LineChannelAccessToken))
            {
                _logger.LogError("LINE notification failed: Config or channel access token not found. ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            // 2. 彙整有效 LineUserId
            var lineUserIds = devices.Select(d => d.LineId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (!lineUserIds.Any())
            {
                _logger.LogWarning("LINE notification send aborted: no valid LineUserIds. ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.LineChannelAccessToken);

            // 3. 發送訊息
            foreach (var userId in lineUserIds)
            {
                try
                {
                    var messageContent = new
                    {
                        to = userId,
                        messages = new[]
                        {
                        new { type = "text", text = $"{title ?? ""}\n{body ?? ""}" }
                    }
                    };
                    var json = JsonConvert.SerializeObject(messageContent);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("LINE message sent. UserId={UserId}, Title={Title}", userId, title);
                    }
                    else
                    {
                        var respText = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("LINE message failed. UserId={UserId}, Status={Status}, Response={Response}", userId, response.StatusCode, respText);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Exception when sending LINE message. UserId={UserId}", userId);
                }
            }
            _logger.LogInformation("LINE notification send finished. ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}", productInfoId, devices.Count);
        }
    }
}
