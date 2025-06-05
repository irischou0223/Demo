using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_scheduled_job")]
    [Comment("通知排程任務")]
    public class NotificationScheduledJob
    {
        [Key]
        [Required]
        [Column("notification_scheduled_job_id", TypeName = "uuid")]
        public Guid NotificationScheduledJobId { get; set; }

        [Required]
        [Column("notification_msg_template_id", TypeName = "uuid")]
        [Comment("通知訊息範本UID")]
        public Guid NotificationMsgTemplateId { get; set; }

        [Required, StringLength(100)]
        [Column("title", TypeName = "varchar")]
        [Comment("任務標題")]
        public string Title { get; set; } = null!;

        [Required]
        [Column("notification_scope", TypeName = "smallint")]
        [Comment("通知範圍 single / group / all")]
        public short NotificationScope { get; set; }

        [Column("notification_target", TypeName = "jsonb")]
        [Comment("通知裝置")]
        public List<Guid>? NotificationTarget { get; set; } = null;

        [Column("notification_group", TypeName = "jsonb")]
        [Comment("通知群組 ")]
        public List<string>? NotificationGroup { get; set; } = null;

        [Required]
        [Column("schedule_type", TypeName = "smallint")]
        [Comment("排程頻率  immediate / daily / monthly / yearly / custom")]
        public short ScheduleType { get; set; }

        [Required]
        [Column("schedule_time", TypeName = "timestamp with time zone")]
        [Comment("排程時間")]
        public DateTime ScheduleTime { get; set; }

        [Required]
        [StringLength(20)]
        [Column("notification_type", TypeName = "varchar")]
        [Comment("通知類型 ")]
        public string NotificationType { get; set; } = null!;

        [Required]
        [Column("is_enabled", TypeName = "boolean")]
        [Comment("是否啟用")]
        public bool IsEnabled { get; set; }

        [Column("next_run_at_utc", TypeName = "timestamp with time zone")]
        [Comment("下次執行時間")]
        public DateTime? NextRunAtUtc { get; set; } = null;

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }

        [Column("cancelled_at_utc", TypeName = "timestamp with time zone")]
        [Comment("取消時間")]
        public DateTime? CancelledAtUtc { get; set; } = null;
    }
}
