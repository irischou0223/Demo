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
        [Column("line_channel_access_token", TypeName = "text")]
        [Comment("Line 頻道存取權杖")]
        public string LineChannelAccessToken { get; set; } = null!;

        [Required, StringLength(256)]
        [Column("smtp_server", TypeName = "varchar")]
        [Comment("SMTP 伺服器位址")]
        public string SmtpServer { get; set; } = null!;

        [Required]
        [Column("smtp_port", TypeName = "integer")]
        [Range(1, 65535, ErrorMessage = "連接埠必須在 1 到 65535 之間")]
        [Comment("SMTP 伺服器連接埠")]
        public int SmtpPort { get; set; }

        [Required, StringLength(254)]
        [Column("user_name", TypeName = "varchar")]
        [EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
        [Comment("SMTP 登入帳號")]
        public string UserName { get; set; } = null!;

        [Required, StringLength(256)]
        [Column("password", TypeName = "varchar")]
        [Comment("SMTP 登入密碼")]
        public string Password { get; set; } = null!;

        [Required, StringLength(254)]
        [Column("from_email", TypeName = "varchar")]
        [EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
        [Comment("寄件人 Email 地址")]
        public string FromEmail { get; set; } = null!;

        [StringLength(100)]
        [Column("from_name", TypeName = "varchar")]
        [Comment("寄件人顯示名稱")]
        public string? FromName { get; set; } = null;

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
