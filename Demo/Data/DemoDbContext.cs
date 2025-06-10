using Demo.Data.Entities;
using Demo.Extensions;
using FirebaseAdmin.Auth;
using Microsoft.EntityFrameworkCore;

namespace Demo.Data
{
    public class DemoDbContext : DbContext
    {
        public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
        {
        }

        public DbSet<UserInfo> UserInfos { get; set; }
        public DbSet<ProductInfo> ProductInfos { get; set; }
        public DbSet<NotificationType> NotificationTypes { get; set; }
        public DbSet<DeviceInfo> DeviceInfos { get; set; }
        public DbSet<ExternalNotificationLog> ExternalNotificationLogs { get; set; }
        public DbSet<BackendNotificationLog> BackendNotificationLogs { get; set; }
        public DbSet<JobNotificationLog> JobNotificationLogs { get; set; }
        public DbSet<CodeInfo> CodeInfos { get; set; }
        public DbSet<NotificationMsgTemplate> NotificationMsgTemplates { get; set; }
        public DbSet<NotificationMsgTemplateData> NotificationMsgTemplateDatas { get; set; }
        public DbSet<NotificationActionConfig> NotificationActionConfigs { get; set; }
        public DbSet<NotificationScheduledJob> NotificationScheduledJobs { get; set; }
        public DbSet<NotificationLimitsConfig> NotificationLimitsConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Primary Key
            modelBuilder.Entity<UserInfo>().HasKey(u => new { u.UserAccount, u.DeviceInfoId, u.ProductInfoId });
            #endregion Primary Key

            #region 預設值
            //modelBuilder.ConfigureUuidDefaults();
            //
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
