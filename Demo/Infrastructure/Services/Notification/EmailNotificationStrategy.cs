using Demo.Config;
using Demo.Data.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// Email 推播策略，SMTP 實作
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

        public async Task SendAsync(List<DeviceInfo> devices, string title, string body, Dictionary<string, string> data, NotificationMsgTemplate template)
        {
            if (devices == null || devices.Count == 0) return;

            var productInfoId = devices.First().ProductInfoId;
            var config = await _configCache.GetNotificationConfigAsync(productInfoId);

            if (config == null)
            {
                _logger.LogError("EmailNotificationStrategy: No config found for ProductInfoId={ProductInfoId}", productInfoId);
                return;
            }

            var emails = devices.Select(d => d.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
            if (!emails.Any())
            {
                _logger.LogWarning("EmailNotificationStrategy: No emails to send.");
                return;
            }

            const int batchSize = 100;
            for (int i = 0; i < emails.Count; i += batchSize)
            {
                var batchEmails = emails.Skip(i).Take(batchSize).ToList();

                // 共用一個 SMTP 連線
                using var client = new SmtpClient();
                try
                {
                    var port = config.SmtpPort;
                    await client.ConnectAsync(config.SmtpServer, port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(config.UserName, config.Password);

                    foreach (var email in batchEmails)
                    {
                        try
                        {
                            var message = new MimeMessage();
                            message.From.Add(new MailboxAddress(config.FromName, config.FromEmail));
                            message.To.Add(MailboxAddress.Parse(email));
                            message.Subject = title;
                            var builder = new BodyBuilder { HtmlBody = body };
                            message.Body = builder.ToMessageBody();

                            await client.SendAsync(message);

                            _logger.LogInformation("EmailNotificationStrategy: Sent email to {Email}, title: {Title}", email, title);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "EmailNotificationStrategy: Failed to send email to {Email}", email);
                        }
                    }

                    await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailNotificationStrategy: SMTP connection or authentication failed.");
                }
            }
        }
    }
}
