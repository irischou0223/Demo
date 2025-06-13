using Demo.Enum;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_log")]
    [Comment("通知發送紀錄")]
    public class NotificationLog
    {
        [Key]
        [Required]
        [Column("notification_log_id", TypeName = "uuid")]
        [Comment("通知發送紀錄UID")]
        public Guid NotificationLogId { get; set; }

        [Column("device_info_id", TypeName = "uuid")]
        [Comment("裝置資訊UID")]
        public Guid DeviceInfoId { get; set; }

        [Column("notification_scheduled_job_id", TypeName = "uuid")]
        [Comment("通知排程任務UID")]
        public Guid? NotificationScheduledJobId { get; set; }

        [Required]
        [Column("notification_source", TypeName = "integer")]
        [Comment("通知來源")]
        public NotificationSourceType NotificationSource { get; set; }

        [Required, StringLength(100)]
        [Column("gw", TypeName = "varchar")]
        [Comment("主機SSID名稱")]
        public string Gw { get; set; } = null!;

        [Required, StringLength(50)]
        [Column("title", TypeName = "varchar")]
        [Comment("通知標題")]
        public string Title { get; set; } = null!;

        [Required, StringLength(255)]
        [Column("body", TypeName = "varchar")]
        [Comment("通知內容")]
        public string Body { get; set; } = null!;

        [Required]
        [Column("notification_status", TypeName = "boolean")]
        [Comment("通知狀態")]
        public bool NotificationStatus { get; set; }

        [Required]
        [Column("result_msg", TypeName = "text")]
        [Comment("通知結果訊息")]
        public string ResultMsg { get; set; } = null!;

        [Required]
        [Column("retry_count", TypeName = "integer")]
        [Comment("重試次數")]
        public int RetryCount { get; set; }

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
