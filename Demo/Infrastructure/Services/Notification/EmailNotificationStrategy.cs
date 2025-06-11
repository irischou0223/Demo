using Demo.Data.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// Email 推播策略，SMTP 實作<br/>
    /// 流程說明：<br/>
    /// 1. 查產品 SMTP 設定<br/>
    /// 2. 彙整有效 Email（已由 NotificationService 分批，每批 devices 皆同產品）<br/>
    /// 3. 逐一寄送，記錄成功／失敗 log
    /// </summary>
    public class EmailNotificationStrategy : INotificationStrategy
    {
        private readonly ILogger<EmailNotificationStrategy> _logger;
        private readonly ConfigCacheService _configCache;

        public EmailNotificationStrategy(ILogger<EmailNotificationStrategy> logger, ConfigCacheService configCache)
        {
            _logger = logger;
            _configCache = configCache;
        }

        /// <summary>
        /// 發送 Email 推播（傳入一批裝置，全部同一產品）
        /// </summary>
        public async Task SendAsync(List<DeviceInfo> devices, string title, string body, Dictionary<string, string> data, NotificationMsgTemplate template)
        {
            _logger.LogInformation("EmailNotificationStrategy.SendAsync 開始, ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}",
             devices.FirstOrDefault()?.ProductInfoId, devices.Count);

            if (devices == null || devices.Count == 0)
            {
                _logger.LogWarning("EmailNotificationStrategy.SendAsync 結束，裝置數量為0");
                return;
            }

            // 1. 查產品對應 SMTP 設定
            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationConfigAsync(productInfoId);

            if (config == null)
            {
                _logger.LogError("EmailNotificationStrategy: 無 SMTP 設定, ProductInfoId={ProductInfoId}", productInfoId);
                _logger.LogWarning("EmailNotificationStrategy.SendAsync 結束，查無 SMTP 設定");
                return;
            }

            // 2. 彙整有效 Email
            var emails = devices.Select(d => d.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
            if (!emails.Any())
            {
                _logger.LogWarning("EmailNotificationStrategy: 無有效 Email, ProductInfoId={ProductInfoId}", productInfoId);
                _logger.LogWarning("EmailNotificationStrategy.SendAsync 結束，無有效 Email");
                return;
            }

            // 3. 建立 SMTP 連線
            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(config.SmtpServer, config.SmtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(config.UserName, config.Password);

                foreach (var email in emails)
                {
                    try
                    {
                        // 4. 組信件
                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(config.FromName, config.FromEmail));
                        message.To.Add(MailboxAddress.Parse(email));
                        message.Subject = title;
                        message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

                        // 5. 發送
                        await client.SendAsync(message);
                        _logger.LogInformation("EmailNotificationStrategy: Sent email to {Email}, title: {Title}", email, title);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogError(ex, "EmailNotificationStrategy: Failed to send email to {Email}", email);
                    }
                }

                await client.DisconnectAsync(true);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "EmailNotificationStrategy: SMTP 連線或認證失敗");
            }
            _logger.LogInformation("EmailNotificationStrategy.SendAsync 結束, ProductInfoId={ProductInfoId}, DeviceCount={DeviceCount}", productInfoId, devices.Count);
        }
    }
}
