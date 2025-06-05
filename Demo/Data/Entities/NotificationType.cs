using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_type")]
    [Comment("通知類型")]
    public class NotificationType
    {
        [Key]
        [Required]
        [Column("notification_type_id", TypeName = "uuid")]
        [Comment("通知類型UID")]
        public Guid NotificationTypeId { get; set; }

        [Required]
        [Column("device_info_id", TypeName = "uuid")]
        [Comment("裝置資訊UID")]
        public Guid DeviceInfoId { get; set; }

        [Column("is_app_active", TypeName = "boolean")]
        [Comment("是否啟用App(iOS、Android)")]
        public bool IsAppActive { get; set; }

        [Column("is_web_active", TypeName = "boolean")]
        [Comment("是否啟用Web")]
        public bool IsWebActive { get; set; }

        [Column("is_email_active", TypeName = "boolean")]
        [Comment("是否啟用Email")]
        public bool IsEmailActive { get; set; }

        [Column("is_line_active", TypeName = "boolean")]
        [Comment("是否啟用Line")]
        public bool IsLineActive { get; set; }
    }
}
