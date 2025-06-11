namespace Demo.Models.DTOs
{
    /// <summary>
    /// 裝置註冊請求 DTO
    /// <para>
    /// 用於裝置註冊 API，承接前端/APP 傳來的所有必要欄位。
    /// 流程說明：
    /// - 用於註冊/異動裝置時，傳遞裝置與推播通道設定等資訊。
    /// - 控制每個通道（App/Web/Email/Line）是否啟用。
    /// - 支援裝置屬性（如 DeviceId、AppVersion、語系等）。
    /// </para>
    /// </summary>
    public class RegisterRequestDto
    {
        /// <summary>
        /// 使用者帳號
        /// </summary>
        public string UserAccount { get; set; } = null!;
        /// <summary>
        /// Firebase 專案 ID，用於區分不同產品/APP
        /// </summary>
        public string FirebaseProjectId { get; set; } = null!;
        /// <summary>
        /// 是否啟用 App 通道通知
        /// </summary>
        public bool IsAppActive { get; set; }
        /// <summary>
        /// 是否啟用 Web 通道推播
        /// </summary>
        public bool IsWebActive { get; set; }
        /// <summary>
        /// 是否啟用 Email 通道推播
        /// </summary>
        public bool IsEmailActive { get; set; }
        /// <summary>
        /// 是否啟用 Line 通道推播
        /// </summary>
        public bool IsLineActive { get; set; }
        /// <summary>
        /// 裝置唯一識別碼（如手機 IMEI、Web UUID 等）
        /// </summary>
        public string DeviceId { get; set; } = null!;
        /// <summary>
        /// App 版本號
        /// </summary>
        public string AppVersion { get; set; } = null!;
        /// <summary>
        /// Firebase Cloud Messaging Token
        /// </summary>
        public string FcmToken { get; set; } = null!;
        /// <summary>
        /// 裝置分群識別（如推播群組）
        /// </summary>
        public string NotificationGroup { get; set; } = null!;
        /// <summary>
        /// 裝置名稱
        /// </summary>
        public string Mobile { get; set; } = null!;
        /// <summary>
        /// 主機 SSID 或裝置 Gateway 識別
        /// </summary>
        public string Gw { get; set; } = null!;
        /// <summary>
        /// 電子郵件
        /// </summary>
        public string Email { get; set; } = null!;
        /// <summary>
        /// Server啟動時指定音效
        /// </summary>
        public string MsgArm { get; set; } = "Home2S_Channel_2";
        /// <summary>
        /// Server解除時指定音效
        /// </summary>
        public string MsgDisarm { get; set; } = "Home2S_Channel_4";
        /// <summary>
        /// Alarm時指定音效
        /// </summary>
        public string Alarm { get; set; } = "Home2S_Channel_0";
        /// <summary>
        /// Panic時指定音效
        /// </summary>
        public string Panic { get; set; } = "Home2S_Channel_3";
        /// <summary>
        /// 語系代碼（預設繁中 "zh-TW"）
        /// </summary>
        public string Lang { get; set; } = "zh-TW";
    }
}
