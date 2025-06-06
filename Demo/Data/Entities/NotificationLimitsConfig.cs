using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_limits_config")]
    [Comment("通知限制設定")]
    public class NotificationLimitsConfig
    {
        [Key]
        [Required]
        [Column("notification_limits_config_id", TypeName = "uuid")]
        [Comment("通知類型UID")]
        public Guid NotificationLimitsConfigId { get; set; }

        [Column("notification_type", TypeName = "integer")]
        [Comment("通知類型: 1=APP, 2=Web, 3=Email, 4=Line ")]
        public int NotificationType { get; set; }

        [Required]
        [Column("max_recipients_per_request", TypeName = "integer")]
        [Comment("最大收件人數量")]
        public int MaxRecipientsPerRequest { get; set; }

        [Required]
        [Column("max_attempts", TypeName = "integer")]
        [Comment("最大重試次數")]
        public int MaxAttempts { get; set; } = 3;

        [Required]
        [Column("initial_retry_delay", TypeName = "integer")]
        [Comment("初始重試延遲(秒)")]
        public int InitialRetryDelay { get; set; }

        [Required]
        [Column("max_retry_delay", TypeName = "integer")]
        [Comment("最大重試延遲(秒)")]
        public int MaxRetryDelay { get; set; }

        [Required]
        [Column("backoff_multiplier", TypeName = "decimal(3,2)")]
        [Comment("退避倍數")]
        public Decimal backoff_multiplier { get; set; }

        [Required]
        [Column("max_retry_duration", TypeName = "integer")]
        [Comment("最大重試持續時間(秒)")]
        public int MaxRetryDuration { get; set; }

        [Required]
        [Column("is_retry_on_timeout", TypeName = "integer")]
        [Comment("超時是否重試")]
        public int IsRetryOnTimeout { get; set; }

        [Required]
        [Column("batch_size", TypeName = "integer")]
        [Comment("單批次處理數量")]
        public int BatchSize { get; set; }

        [Required]
        [Column("max_concurrent_tasks", TypeName = "integer")]
        [Comment("最大併發任務數")]
        public int MaxConcurrentTasks { get; set; }

        [Required]
        [Column("rate_limit_per_second", TypeName = "integer")]
        [Comment("每秒執行限制")]
        public int RateLimitPerSecond { get; set; }

        [Required]
        [Column("rate_limit_per_minute", TypeName = "integer")]
        [Comment("每分鐘執行限制")]
        public int RateLimitPerMinute { get; set; }

        [Required]
        [Column("batch_interval_ms", TypeName = "integer")]
        [Comment("批次間隔(毫秒)")]
        public int BatchIntervalMs { get; set; }

        [Required]
        [Column("request_timeout_ms", TypeName = "integer")]
        [Comment("單次請求超時(毫秒)")]
        public int RequestTimeoutMs { get; set; }

        [Required]
        [Column("queue_max_size", TypeName = "integer")]
        [Comment("佇列最大容量")]
        public int QueueMaxSize { get; set; }

        [Required]
        [Column("immediate_delay", TypeName = "integer")]
        [Comment("即時推播延遲(秒)")]
        public int ImmediateDelay { get; set; }

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
