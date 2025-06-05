using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("notification_msg_template")]
    [Comment("通知訊息範本")]
    public class NotificationMsgTemplate
    {
        [Key]
        [Required]
        [Column("notification_msg_template_id", TypeName = "uuid")]
        [Comment("通知訊息範本UID")]
        public Guid NotificationMsgTemplateId { get; set; }

        [Required]
        [Column("code_info_id", TypeName = "uuid")]
        [Comment("代碼資訊UID")]
        public Guid CodeInfoId { get; set; }

        [Required, StringLength(100)]
        [Column("gw", TypeName = "varchar")]
        [Comment("主機SSID名稱")]
        public string Gw { get; set; } = null!;

        [StringLength(100)]
        [Column("sound", TypeName = "varchar")]
        [Comment("通知聲音，APP專用")]
        public string Sound { get; set; } = "default";

        [StringLength(10)]
        [Column("badge", TypeName = "varchar")]
        [Comment("未讀數(iOS專用)")]
        public string? Badge { get; set; } = null;

        [StringLength(255)]
        [Column("click_action_app", TypeName = "varchar")]
        [Comment("點擊通知要執行的動作(給對應的行為)")]
        public string? ClickActionApp { get; set; } = null;

        [StringLength(255)]
        [Column("icon", TypeName = "varchar")]
        [Comment("通知的Icon(Web專用))")]
        public string? Icon { get; set; } = null;

        [StringLength(255)]
        [Column("click_action_web", TypeName = "varchar")]
        [Comment("點擊通知要執行的動作，完整URL")]
        public string? ClickActionWeb { get; set; } = null;

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
