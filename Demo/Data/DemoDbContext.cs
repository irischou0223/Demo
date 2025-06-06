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
            modelBuilder.ConfigureUuidDefaults();
            //
            // APP (Firebase Server) - ID 1
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = 1,
                    MaxAttempts = 5,
                    InitialRetryDelay = 5,
                    MaxRetryDelay = 300,
                    BackoffMultiplier = 2.0m, // 注意 'm' 表示 decimal
                    MaxRetryDuration = 3600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 50,
                    RateLimitPerSecond = 1000,
                    RateLimitPerMinute = 15000,
                    MaxRecipientsPerRequest = 500,
                    BatchIntervalMs = 10,
                    RequestTimeoutMs = 5000,
                    QueueMaxSize = 10000,
                    ImmediateDelay = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // WEB (Firebase Server) - ID 2
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = 2,
                    MaxAttempts = 3,
                    InitialRetryDelay = 10,
                    MaxRetryDelay = 60,
                    BackoffMultiplier = 1.5m,
                    MaxRetryDuration = 600,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 30,
                    RateLimitPerSecond = 500,
                    RateLimitPerMinute = 10000,
                    MaxRecipientsPerRequest = 500,
                    BatchIntervalMs = 20,
                    RequestTimeoutMs = 3000,
                    QueueMaxSize = 5000,
                    ImmediateDelay = 0,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // Email - ID 3
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = 3,
                    MaxAttempts = 7,
                    InitialRetryDelay = 60,
                    MaxRetryDelay = 3600,
                    BackoffMultiplier = 2.5m,
                    MaxRetryDuration = 86400,
                    IsRetryOnTimeout = false,
                    BatchSize = 200,
                    MaxConcurrentTasks = 10,
                    RateLimitPerSecond = 50,
                    RateLimitPerMinute = 2000,
                    MaxRecipientsPerRequest = 1,
                    BatchIntervalMs = 1000,
                    RequestTimeoutMs = 10000,
                    QueueMaxSize = 20000,
                    ImmediateDelay = 300,
                    CreateAtUtc = DateTime.UtcNow
                }
            );

            // LINE - ID 4
            modelBuilder.Entity<NotificationLimitsConfig>().HasData(
                new NotificationLimitsConfig
                {
                    NotificationLimitsConfigId = Guid.NewGuid(),
                    NotificationType = 4,
                    MaxAttempts = 4,
                    InitialRetryDelay = 30,
                    MaxRetryDelay = 180,
                    BackoffMultiplier = 1.8m,
                    MaxRetryDuration = 1800,
                    IsRetryOnTimeout = true,
                    BatchSize = 500,
                    MaxConcurrentTasks = 40,
                    RateLimitPerSecond = 800,
                    RateLimitPerMinute = 12000,
                    MaxRecipientsPerRequest = 500,
                    BatchIntervalMs = 50,
                    RequestTimeoutMs = 4000,
                    QueueMaxSize = 8000,
                    ImmediateDelay = 10,
                    CreateAtUtc = DateTime.UtcNow
                }
            );
            #endregion 預設值

            base.OnModelCreating(modelBuilder);
        }
    }
}
