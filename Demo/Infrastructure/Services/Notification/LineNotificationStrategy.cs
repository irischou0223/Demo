using Demo.Data;
using Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// LINE 推播策略，使用LINE官方帳號發送通知
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

        public async Task SendAsync(
            List<DeviceInfo> devices,
            string title,
            string body,
            Dictionary<string, string> data,
            NotificationMsgTemplate template)
        {
            if (devices == null || devices.Count == 0) return;

            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetConfigAsync(productInfoId);
            if (config == null || string.IsNullOrWhiteSpace(config.LineChannelAccessToken))
            {
                _logger.LogError("LineNotificationStrategy: No config/token found for ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            var lineUserIds = devices.Select(d => d.LineId)
                                     .Where(id => !string.IsNullOrWhiteSpace(id))
                                     .Distinct()
                                     .ToList();
            if (lineUserIds.Count == 0)
            {
                _logger.LogWarning("LineNotificationStrategy: No valid LineUserIds.");
                return;
            }

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

                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.LineChannelAccessToken);

                    var json = JsonConvert.SerializeObject(messageContent);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Line message sent to {UserId}, title: {Title}", userId, title);
                    }
                    else
                    {
                        var respText = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("Failed to send Line message to {UserId}, status: {Status}, resp: {Resp}", userId, response.StatusCode, respText);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Exception when sending LINE message to {UserId}", userId);
                }
            }
        }
    }
}
