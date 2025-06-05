using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_action_config")]
    [Comment("通知行為設定")]
    public class NotificationActionConfig
    {
        [Key]
        [Required]
        [Column("notification_action_config_id", TypeName = "uuid")]
        public Guid NotificationActionConfigId { get; set; }

        [Required]
        [Column("product_info_id", TypeName = "uuid")]
        [Comment("產品資訊UID")]
        public Guid ProductInfoId { get; set; }

        [Required]
        [Column("fcm_key", TypeName = "text")]
        [Comment("FCM金鑰")]
        public string FcmKey { get; set; } = null!;

        [Required]
        [Range(1, 10)] // 1分鐘到10分鐘
        [Column("retry_delay_minutes", TypeName = "smallint")]
        [Comment("重試延遲時間(分鐘)")]
        public short RetryDelayMinutes { get; set; } = 3;

        [Required]
        [Range(1, 10)] // 合理的重試次數範圍
        [Column("max_retry_count", TypeName = "smallint")]
        [Comment("最多重試次數")]
        public short MaxRetryCount { get; set; } = 5;

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
