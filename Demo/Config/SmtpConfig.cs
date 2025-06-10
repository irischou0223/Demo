using System.ComponentModel.DataAnnotations;

namespace Demo.Config
{
    /// <summary>
    /// SmtpConfig 類別用於儲存發送電子郵件所需的 SMTP 伺服器設定。
    /// 這包含了連接到郵件伺服器、身份驗證以及發件人資訊等細節。
    /// </summary>
    public class SmtpConfig
    {
        /// <summary>
        /// 郵件伺服器的主機名稱或 IP 位址。
        /// 例如："smtp.gmail.com" 或 "mail.yourcompany.com"。
        /// 這是您要連接的 SMTP 伺服器位址。
        /// </summary>
        [Required]
        public string Host { get; set; } = null!;

        /// <summary>
        /// 郵件伺服器的連接埠號碼。
        /// 常見的連接埠有：
        /// - 25 (非加密或明文傳輸)
        /// - 587 (TLS/STARTTLS 加密，推薦用於提交郵件)
        /// - 465 (SSL/TLS 加密，較舊的標準，但仍在使用)
        /// </summary>
        [Required]
        public int Port { get; set; }

        /// <summary>
        /// 用於登入 SMTP 伺服器的使用者名稱。
        /// 通常是您的電子郵件地址。
        /// 如果伺服器不需要身份驗證，則可以留空。
        /// </summary>
        [Required]
        public string Username { get; set; } = null!;

        /// <summary>
        /// 用於登入 SMTP 伺服器的密碼。
        /// 如果伺服器不需要身份驗證，則可以留空。
        /// **請注意：在實際應用中，不建議將密碼直接硬編碼在程式碼或純文字設定檔中。
        /// 應使用更安全的機制，如環境變數、Azure Key Vault 或其他密碼管理系統來儲存敏感資訊。**
        /// </summary>
        [Required]
        public string Password { get; set; } = null!;

        /// <summary>
        /// 發件人的電子郵件地址。
        /// 這將會顯示在收件人看到的「寄件者」欄位中。
        /// 例如："noreply@yourcompany.com"。
        /// </summary>
        [Required]
        public string FromAddress { get; set; } = null!;

        /// <summary>
        /// 發件人的顯示名稱。
        /// 這將會顯示在收件人看到的「寄件者」名稱欄位中，讓收件人更容易識別寄件來源。
        /// 例如："您的公司名稱" 或 "系統通知"。
        /// </summary>
        [Required]
        public string FromName { get; set; } = null!;
    }
}
