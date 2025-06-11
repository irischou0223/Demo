using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("device_info")]
    [Comment("裝置資訊")]
    public class DeviceInfo
    {
        [Key]
        [Required]
        [Column("device_info_id", TypeName = "uuid")]
        [Comment("裝置資訊UID")]
        public Guid DeviceInfoId { get; set; }

        [Required, StringLength(100)]
        [Column("device_id", TypeName = "varchar")]
        [Comment("個別裝置ID")]
        public string DeviceId { get; set; } = null!;

        [Required]
        [Column("product_info_id", TypeName = "uuid")]
        [Comment("產品資訊UID")]
        public Guid ProductInfoId { get; set; }

        [Required, StringLength(50)]
        [Column("app_version", TypeName = "varchar")]
        [Comment("APP版號")]
        public string AppVersion { get; set; } = null!;

        [Required, StringLength(255)]
        [Column("fcm_token", TypeName = "varchar")]
        [Comment("FCM Registration Token")]
        public string FcmToken { get; set; } = null!;

        [Required, StringLength(100)]
        [Column("notification_group", TypeName = "varchar")]
        [Comment("通知群組")]
        public string NotificationGroup { get; set; } = null!;

        [StringLength(100)]
        [Column("mobile", TypeName = "varchar")]
        [Comment("裝置名稱")]
        public string? Mobile { get; set; } = null;

        [Required, StringLength(100)]
        [Column("gw", TypeName = "varchar")]
        [Comment("主機SSID名稱")]
        public string Gw { get; set; } = null!;

        [Required, StringLength(254)]
        [Column("email", TypeName = "varchar")]
        [Comment("電子郵件")]
        public string Email { get; set; } = null!;

        [Required, StringLength(20)]
        [Column("line_id", TypeName = "varchar")]
        [Comment("Line ID")]
        public string LineId { get; set; } = null!;

        [StringLength(100)]
        [Column("msgarm", TypeName = "varchar")]
        [Comment("Server啟動時指定音效")]
        public string MsgArm { get; set; } = "Home2S_Channel_2";

        [StringLength(100)]
        [Column("msgdisarm", TypeName = "varchar")]
        [Comment("Server解除時指定音效")]
        public string MsgDisArm { get; set; } = "Home2S_Channel_4";

        [StringLength(100)]
        [Column("alarm", TypeName = "varchar")]
        [Comment("Alarm時指定音效")]
        public string Alarm { get; set; } = "Home2S_Channel_0";

        [StringLength(100)]
        [Column("panic", TypeName = "varchar")]
        [Comment("Panic時指定音效")]
        public string Panic { get; set; } = "Home2S_Channel_3";

        [Required]
        [Column("status", TypeName = "boolean")]
        [Comment("裝置狀態")]
        public bool Status { get; set; } = true;

        [Required, StringLength(10)]
        [Column("lang", TypeName = "varchar")]
        [Comment("語系")]
        public string Lang { get; set; } = "zh-TW";

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("註冊時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
