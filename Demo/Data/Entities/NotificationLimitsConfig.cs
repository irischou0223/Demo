using Demo.Enum;
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
        [Comment("通知類型: 1=APP, 2=Web, 3=Email, 4=Line")]
        public NotificationChannelType NotificationType { get; set; }

        [Required]
        [Column("max_recipients_per_request", TypeName = "integer")]
        [Comment("單次API請求最大收件人數量 (服務供應商限制，如FCM的SendAll內部批次上限)")]
        public int MaxRecipientsPerRequest { get; set; }

        // --- 重試策略設定 ---
        [Required]
        [Column("max_attempts", TypeName = "integer")]
        [Comment("最大重試次數")]
        public int MaxAttempts { get; set; } = 3;

        [Required]
        [Column("initial_retry_delay_seconds", TypeName = "integer")]
        [Comment("初始重試延遲(秒)")]
        public int InitialRetryDelaySeconds { get; set; }

        [Required]
        [Column("max_retry_delay_seconds", TypeName = "integer")]
        [Comment("最大重試延遲(秒)")]
        public int MaxRetryDelaySeconds { get; set; }

        [Required]
        [Column("backoff_multiplier", TypeName = "decimal(3,2)")]
        [Comment("退避倍數 (例如 2.0 代表指數增長)")]
        public Decimal BackoffMultiplier { get; set; }

        [Required]
        [Column("max_retry_duration_seconds", TypeName = "integer")]
        [Comment("最大重試持續時間(秒)")]
        public int MaxRetryDurationSeconds { get; set; }

        [Required]
        [Column("is_retry_on_timeout", TypeName = "boolean")]
        [Comment("超時是否重試")]
        public bool IsRetryOnTimeout { get; set; }
        // --- 重試策略設定結束 ---

        // --- 應用程式層面的發送控制設定 ---
        [Required]
        [Column("batch_size", TypeName = "integer")]
        [Comment("應用程式層面，單批次處理並發送的收件人數量")]
        public int BatchSize { get; set; }

        [Required]
        [Column("max_concurrent_tasks", TypeName = "integer")]
        [Comment("最大併發任務數")]
        public int MaxConcurrentTasks { get; set; }

        [Required]
        [Column("rate_limit_per_second", TypeName = "integer")]
        [Comment("每秒允許的總請求/操作數量")]
        public int RateLimitPerSecond { get; set; }

        [Required]
        [Column("request_timeout_ms", TypeName = "integer")]
        [Comment("單次外部 API 請求超時(毫秒)")]
        public int RequestTimeoutMs { get; set; }

        [Required]
        [Column("queue_max_size", TypeName = "integer")]
        [Comment("內部發送佇列的最大容量")]
        public int QueueMaxSize { get; set; }

        [Required]
        [Column("initial_dispatch_delay_seconds", TypeName = "integer")]
        [Comment("排程或即時推播訊息的初始分派延遲(秒)。0 表示立即處理。")]
        public int InitialDispatchDelaySeconds { get; set; }
        // --- 應用程式層面的發送控制設定結束 ---

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間 (UTC)")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間 (UTC)")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
