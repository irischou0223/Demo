using Demo.Enum;
using Hangfire.Common;

namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播請求 DTO
    /// ---
    /// 用於發送通知的統一資料模型，支援一般通知與排程通知兩種模式。
    /// <br/>
    /// 一般通知：
    ///   - 指定 DeviceIds 或 NotificationGroup 為推播目標
    ///   - 通常需指定 Code（用於查找標準訊息模板）
    /// 排程通知：
    ///   - 通過 NotificationMsgTemplateId 指定已預設的訊息模板
    ///   - 可額外自訂 Title/Body 覆蓋模板內容
    /// </summary>
    public class NotificationRequestDto
    {
        /// <summary>
        /// 推播來源（如 Backend、External、Job）
        /// </summary>
        public NotificationSourceType Source { get; set; }

        #region 一般通知條件

        /// <summary>
        /// 目標裝置 DeviceInfoId 清單（指定裝置推播）
        /// </summary>
        public List<Guid>? DeviceInfoIds { get; set; }
        /// <summary>
        /// 目標裝置群組（指定群組推播）
        /// </summary>
        public string? NotificationGroup { get; set; }
        /// <summary>
        /// 語系，可選，預設 zh-TW
        /// </summary>
        public string Lang { get; set; }
        /// <summary>
        /// 通知代碼（用於查找標準訊息模板，一般通知必填）
        /// </summary>
        public string Code { get; set; }

        #endregion 一般通知條件

        #region 排程通知專用

        public Guid? NotificationScheduledJobId { get; set; }

        /// <summary>
        /// 通知訊息範本 ID（排程推播用，支援自訂或預設模板）
        /// </summary>
        public Guid? NotificationMsgTemplateId { get; set; }
        /// <summary>
        /// 自訂標題（可覆蓋模板/代碼內容，排程推播可用）
        /// </summary>
        public string? Title { get; set; }
        /// <summary>
        /// 自訂內容（可覆蓋模板/代碼內容，排程推播可用）
        /// </summary>
        public string? Body { get; set; }

        #endregion 排程通知專用
    }
}
