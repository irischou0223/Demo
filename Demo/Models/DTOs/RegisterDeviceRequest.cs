namespace Demo.Models.DTOs
{
    /// <summary>
    /// 裝置註冊請求DTO
    /// </summary>
    public class RegisterDeviceRequest
    {
        public string UserAccount { get; set; } = null!;
        public string FirebaseProjectId { get; set; } = null!;
        public bool IsAppActive { get; set; }
        public bool IsWebActive { get; set; }
        public bool IsEmailActive { get; set; }
        public bool IsLineActive { get; set; }
        public string DeviceId { get; set; } = null!;
        public string AppVersion { get; set; } = null!;
        public string FcmToken { get; set; } = null!;
        public string NotificationGroup { get; set; } = null!;
        public string Mobile { get; set; } = null!;
        public string Gw { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string MsgArm { get; set; } = "default: Home2S_Channel_2";
        public string MsgDisarm { get; set; } = "default: Home2S_Channel_4";
        public string Alarm { get; set; } = "default: Home2S_Channel_0";
        public string Panic { get; set; } = "default: Home2S_Channel_3";
        public string Lang { get; set; } = "zh-TW";
    }
}
