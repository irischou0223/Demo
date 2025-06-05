using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notificatio_msg_template_data")]
    [Comment("通知訊息範本-APP自訂資料")]
    public class NotificationMsgTemplateData
    {
        [Key]
        [Required]
        [Column("notification_msg_template_data", TypeName = "uuid")]
        [Comment("通知訊息範本-APP自訂資料UID")]
        public Guid NotificationMsgTemplateDataId { get; set; }

        [Required]
        [Column("notification_msg_template_id", TypeName = "uuid")]
        [Comment("通知訊息範本UID")]
        public Guid NotificationMsgTemplateId { get; set; }

        [Required, StringLength(255)]
        [Column("key", TypeName = "varchar")]
        [Comment("自訂鍵")]
        public string Key { get; set; } = null!;

        [Required, StringLength(255)]
        [Column("value", TypeName = "varchar")]
        [Comment("自訂值")]
        public string Value { get; set; } = null!;

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
