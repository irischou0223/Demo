using Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Data
{
    /// <summary>
    /// DemoDbContext
    /// ---
    /// 系統主要資料庫存取入口，包含所有通知、裝置、用戶、產品等相關表格的 DbSet 定義。
    /// 支援多通知通道（App/Web/Email/Line），並於 OnModelCreating 設定主鍵、預設值與資料初始化。
    /// </summary>
    public class DemoDbContext : DbContext
    {
        public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
        {
            //this.Database.EnsureCreated();
        }

        /// <summary>使用者資訊</summary>
        public DbSet<UserInfo> UserInfos { get; set; }
        /// <summary>產品資訊</summary>
        public DbSet<ProductInfo> ProductInfos { get; set; }
        /// <summary>通知型別（頻道）</summary>
        public DbSet<NotificationType> NotificationTypes { get; set; }
        /// <summary>裝置資訊</summary>
        public DbSet<DeviceInfo> DeviceInfos { get; set; }
        /// <summary>外部通知發送紀錄</summary>
        public DbSet<ExternalNotificationLog> ExternalNotificationLogs { get; set; }
        /// <summary>後台通知發送紀錄</summary>
        public DbSet<BackendNotificationLog> BackendNotificationLogs { get; set; }
        /// <summary>排程觸發通知紀錄</summary>
        public DbSet<JobNotificationLog> JobNotificationLogs { get; set; }
        /// <summary>通知代碼</summary>
        public DbSet<CodeInfo> CodeInfos { get; set; }
        /// <summary>通知訊息模板</summary>
        public DbSet<NotificationMsgTemplate> NotificationMsgTemplates { get; set; }
        /// <summary>通知訊息模板明細</summary>
        public DbSet<NotificationMsgTemplateData> NotificationMsgTemplateDatas { get; set; }
        /// <summary>通知行為設定</summary>
        public DbSet<NotificationActionConfig> NotificationActionConfigs { get; set; }
        /// <summary>通知限制設定</summary>
        public DbSet<NotificationLimitsConfig> NotificationLimitsConfigs { get; set; }
        /// <summary>通知排程任務</summary>
        public DbSet<NotificationScheduledJob> NotificationScheduledJobs { get; set; }

        /// <summary>
        /// 資料表主鍵、預設值與種子資料設定
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 全域預設 schema 設為 public
            modelBuilder.HasDefaultSchema("public");

            #region Primary Key

            // 複合主鍵：UserAccount, DeviceInfoId, ProductInfoId
            modelBuilder.Entity<UserInfo>().HasKey(u => new { u.UserAccount, u.DeviceInfoId, u.ProductInfoId });

            #endregion Primary Key

            #region 預設值

            Guid guidProductTest = Guid.NewGuid();
            Guid guidDeviceTest = Guid.NewGuid();
            Guid guidCodeTest = Guid.NewGuid();

            #region TEST
            //TEST
            modelBuilder.Entity<ProductInfo>().HasData(
                new ProductInfo
                {
                    ProductInfoId = guidProductTest,
                    ProductName = "notify-hub-demo",
                    ProductCode = "demo",
                    FirebaseProjectId = "notification-service-52fac"
                }
            );

            modelBuilder.Entity<DeviceInfo>().HasData(
                new DeviceInfo
                {
                    DeviceInfoId = guidDeviceTest,
                    DeviceId = string.Empty,
                    ProductInfoId = guidProductTest,
                    AppVersion = "1.0.0",
                    FcmToken = "csz-9nUiPZMP8dfE_QwUzS:APA91bGqBcCnpfQtN3E7CI_EKOptGWixK_1TYaqLJpgfORlXqGDY8cdgkSjyxzvQ_Ar2S_V1W14ViWtbV8jmdlbtYvB-SlDWqtENIj_zeA-nooyiss8zXbo",
                    NotificationGroup = "8F-D2-A6-1C-4E-9B",
                    Mobile = null,
                    Gw = "Starlink_AP_7C5F",
                    Email = "00176@etechpro.com.tw",
                    LineId = string.Empty,
                    Status = true,
                    CreateAtUtc = DateTime.UtcNow,
                }
            );

            modelBuilder.Entity<UserInfo>().HasData(
                new UserInfo
                {
                    UserAccount = "00176",
                    DeviceInfoId = guidDeviceTest,
                    ProductInfoId = guidProductTest,
                    DeviceId = string.Empty
                }
            );

            modelBuilder.Entity<NotificationType>().HasData(
                new NotificationType
                {
                    NotificationTypeId = Guid.NewGuid(),
                    DeviceInfoId = guidDeviceTest,
                    IsAppActive = false,
                    IsWebActive = true,
                    IsEmailActive = false,
                    IsLineActive = false,
                }
            );

            modelBuilder.Entity<NotificationActionConfig>().HasData(
                new NotificationActionConfig
                {
                    NotificationActionConfigId = Guid.NewGuid(),
                    ProductInfoId = guidProductTest,
                    FcmKey = "{\r\n  \"type\": \"service_account\",\r\n  \"project_id\": \"notification-service-52fac\",\r\n  \"private_key_id\": \"b5870e8b664357f018b6b337255b637a14ff5afa\",\r\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC9Gbcdpl+7cg+T\\n8iCkTo9KsPfNh3PDt0rhGYynpWj9zEsgHdnmDR508Eqspg/05bzV9H2WZTSBx0z/\\nTIytgWjHmFQRoNUGrjD+77Ar70H8nHGZx0kRCAYpUajUea3t/4Qvu5oaEzHRM54a\\nOFhTZRB/n/2NLXuPlO8k9xoei0MwmbeEh0nRWwcUrl0cgYg+2GYuGGAygeBgOsF0\\n/tNRWEDheDomrmH8Gy729FIvGu/KqBc+2ZJsh/SlrjnZbRfqtMbZrtZAqxGMWLPv\\nzCLWCTiUGtIXgYLTGbM1C0mF5Qfp34lFw/QUzFWzWlbuY03QGlZJsJdRpLOWRayf\\nvyapbG3FAgMBAAECggEAA1Wuwdo9h6hosw99K00qySdEDkyqDtATO/fWhJQJT2B2\\nn0M5yVY145KaXoPpddaz4qe7GYVKOlMWRxbX7D9eixaHviRpXUvv8vgbGu/PMc2B\\nm1hXeTtqIq/T9zavQIXDlGvpFKQQTI0AMQot5tqVsC8MGC9A3JAyX2w4sWigb486\\nCX1MXz7zT9XmfZNdIqGNmLNbhzd7dYDaiNrgEyHq2JYHNBp9HZNacQhWSXm6wKwR\\n2orsntc4ilXrHHTI7FBxiNiKkzgylmXYpxXpZrD93V6F2qqAQY5Q2V+ITVx84wuP\\n7hfsAzintREIq5+DxkUBZPOo868mlI52NaLMgKeZiQKBgQDvjwjdR1wIFrIA4sMM\\nF4VMDnUy9q4uvmLsuw3166OdZZexjOjkU2GKQnmkefoPv6wG9MGC3pkk4M7oAFGC\\n0BmE7kci8eyL10LJtzX6eBXZpwVERjErqKcM0j1vSeLqiJZYE/3PQo2Nz5Cbg4zz\\nGrBJxIpgyAidGURRIwKoYcX3awKBgQDKFCVYWy+7GZBhyM6n4wLNbPQyrQPlb8a8\\nG8nM0Gk0PRdzbu4rDdGgMHpSrXeHejB56HVHjZaZP+3fNrvWrhTN0R+uK8PBMVm3\\ngsL7/r3lVUwG8NDdMnw+SGRL9HHDXIDL0ezqeDp2HrwewrQPIJP07FQODUoSP+Qz\\nqE7pOyjrjwKBgCR3KWpFioTQr5fi7L3Sdr/1E3IGis0ivfw7HQzqKaWz6Ttlr63R\\n428gX1PiHWZ4Tr9gUnSRXc53SgeWxNGcy8WoX7u6B7/hrJD4Codt8CWJfwu9g46Z\\nxZP2DNP780awM/KEWIZMIzALAIArrjDzRxJzkHza9jSzu+p94dGv0GqxAoGAALMv\\nKe4u7SP6hwwuAxDbOqDs+5vzzoCjnJUwDsCODLtFcIXq10VV+4sPcWfeaR64OkPe\\n3B+WbPN2vHYxEl5J/iiCRpUqOWoVWHhgeoT4XWn9OGzvHEUHfyO7DTRMjJOucoZI\\nnstJ0Izss+KSwxamzIthAydyoTuNa8xicZZd3usCgYEA0AQshK8VaUFMcxciSxrI\\nBXvxOEWM/PgAxJBaDoZFeUBqxLi0J4kHvXpmJwAjJRzhODBUahfwx2BKcZ0O+NGr\\ny61eMP219Rx1yRdKxTwzxtt9veYdApgau7bY+b6HQEa2y1zvdDutbNy+xOtYLpZu\\nVGkqPnjQLHJSCKopxpec3NU=\\n-----END PRIVATE KEY-----\\n\",\r\n  \"client_email\": \"firebase-adminsdk-fbsvc@notification-service-52fac.iam.gserviceaccount.com\",\r\n  \"client_id\": \"104301856267253948837\",\r\n  \"auth_uri\": \"https://accounts.google.com/o/oauth2/auth\",\r\n  \"token_uri\": \"https://oauth2.googleapis.com/token\",\r\n  \"auth_provider_x509_cert_url\": \"https://www.googleapis.com/oauth2/v1/certs\",\r\n  \"client_x509_cert_url\": \"https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk-fbsvc%40notification-service-52fac.iam.gserviceaccount.com\",\r\n  \"universe_domain\": \"googleapis.com\"\r\n}",
                    LineChannelAccessToken = "",
                    SmtpServer = "smtp.office365.com",
                    SmtpPort =587,
                    UserName= "service2@sks.com.tw",
                    Password= "jgptzdppclxcdnpc",
                    FromEmail= "service2@sks.com.tw",    
                    FromName = "Notify Hub Test",
                    CreateAtUtc= DateTime.UtcNow,
                }
            );

            modelBuilder.Entity<CodeInfo>().HasData(
                new CodeInfo
                {
                    CodeInfoId = guidCodeTest,
                    Code="Demo-A",
                    Lang  = "zh-TW",
                    Value="測試",
                    Title= "Notify Hub Demo",
                    Body= "Notify Hub 測試",
                    Desc="通知測試",
                    CreateAtUtc = DateTime.UtcNow,
                }
            );

            modelBuilder.Entity<NotificationMsgTemplate>().HasData(
              new NotificationMsgTemplate
              {
                  NotificationMsgTemplateId = Guid.NewGuid(),
                  CodeInfoId = guidCodeTest,
                  Gw = "Starlink_AP_7C5F",
                  Sound = "default",
                  CreateAtUtc = DateTime.UtcNow,
              }
          );

            #endregion TEST

            // APP (Firebase Server) - ID 1
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = Enum.NotificationChannelType.App,
                    MaxRecipientsPerRequest = 500,
                    MaxAttempts = 5,
                    InitialRetryDelaySeconds = 5,
                    MaxRetryDelaySeconds = 300,
                    BackoffMultiplier = 2.0m,
                    MaxRetryDurationSeconds = 3600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 50,
                    RateLimitPerSecond = 1000,
                    RequestTimeoutMs = 5000,
                    QueueMaxSize = 10000,
                    InitialDispatchDelaySeconds = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // WEB (Firebase Server) - ID 2
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = Enum.NotificationChannelType.Web,
                    MaxRecipientsPerRequest = 500,
                    MaxAttempts = 5,
                    InitialRetryDelaySeconds = 5,
                    MaxRetryDelaySeconds = 300,
                    BackoffMultiplier = 2.0m,
                    MaxRetryDurationSeconds = 3600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 50,
                    RateLimitPerSecond = 1000,
                    RequestTimeoutMs = 5000,
                    QueueMaxSize = 10000,
                    InitialDispatchDelaySeconds = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // Email - ID 3
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = Enum.NotificationChannelType.Email,
                    MaxRecipientsPerRequest = 500,
                    MaxAttempts = 5,
                    InitialRetryDelaySeconds = 5,
                    MaxRetryDelaySeconds = 300,
                    BackoffMultiplier = 2.0m,
                    MaxRetryDurationSeconds = 3600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 50,
                    RateLimitPerSecond = 1000,
                    RequestTimeoutMs = 5000,
                    QueueMaxSize = 10000,
                    InitialDispatchDelaySeconds = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // LINE - ID 4
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = Enum.NotificationChannelType.Line,
                    MaxRecipientsPerRequest = 500,
                    MaxAttempts = 5,
                    InitialRetryDelaySeconds = 5,
                    MaxRetryDelaySeconds = 300,
                    BackoffMultiplier = 2.0m,
                    MaxRetryDurationSeconds = 3600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 50,
                    RateLimitPerSecond = 1000,
                    RequestTimeoutMs = 5000,
                    QueueMaxSize = 10000,
                    InitialDispatchDelaySeconds = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            #endregion 預設值

            base.OnModelCreating(modelBuilder);
        }
    }
}
