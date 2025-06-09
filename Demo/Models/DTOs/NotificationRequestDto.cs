using Demo.Enum;

namespace Demo.Models.DTOs
{
    public class NotificationRequestDto
    {
        #region 一般通知條件

        /// <summary>
        /// 目標裝置 DeviceId 清單（指定裝置推播）
        /// </summary>
        public List<string> DeviceIds { get; set; }
        /// <summary>
        /// 目標裝置群組（指定群組推播）
        /// </summary>
        public string? NotificationGroup { get; set; }
        /// <summary>
        /// 語系，可選，預設 zh-TW
        /// </summary>
        public string Lang { get; set; }
        /// <summary>
        /// 對應通知代碼（用於 codeInfo 查模板）
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 推播來源
        /// </summary>
        public NotificationSourceType Source { get; set; }

        #endregion 一般通知條件

        #region 排程通知專用
        /// <summary>
        /// 通知訊息範本 ID（排程推播用）
        /// </summary>
        public Guid? NotificationMsgTemplateId { get; set; }
        /// <summary>
        /// 自訂標題（可覆蓋模板/代碼內容）
        /// </summary>
        public string? Title { get; set; }
        /// <summary>
        /// 自訂內容（可覆蓋模板/代碼內容）
        /// </summary>
        public string? Body { get; set; }
        #endregion 排程通知專用
    }
}
