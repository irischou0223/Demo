namespace Demo.Models.DTOs
{
    public class NotificationRequestDto
    {
        /// <summary>
        /// 裝置ID清單，支援單一/多個，如為 null 則依 notification_group 或全發
        /// </summary>
        public List<string> DeviceIds { get; set; }
        /// <summary>
        /// 通知訊息模板代碼
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 通知群組名稱（可選，指定群發用）
        /// </summary>
        public string NotificationGroup { get; set; }
        /// <summary>
        /// 語系，可選，預設 zh-TW
        /// </summary>
        public string Lang { get; set; } 
    }
}
