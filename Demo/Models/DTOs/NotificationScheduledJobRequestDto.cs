using Demo.Enum;

namespace Demo.Models.DTOs
{
    /// <summary>
    /// 通知排程任務請求 DTO
    /// 用於建立、修改排程時的輸入參數
    /// </summary>
    public class NotificationScheduledJobRequestDto
    {
        /// <summary>
        /// 通知訊息範本UID
        /// </summary>
        public Guid NotificationMsgTemplateId { get; set; }

        /// <summary>
        /// 任務標題
        /// </summary>
        public string Title { get; set; } = null!;

        /// <summary>
        /// 通知範圍 1=single, 2=group, 3=all
        /// </summary>
        public NotificationScopeType NotificationScope { get; set; }

        /// <summary>
        /// 通知裝置（可多筆 Guid）
        /// </summary>
        public List<Guid>? NotificationTarget { get; set; } = null;

        /// <summary>
        /// 通知群組（可多筆群組識別字串）
        /// </summary>
        public List<string>? NotificationGroup { get; set; } = null;

        /// <summary>
        /// 排程頻率類型（0=immediate, 1=daily, 2=monthly, 3=yearly, 9=custom）
        /// </summary>
        public ScheduleFrequencyType ScheduleFrequencyType { get; set; }

        /// <summary>
        /// 排程時間
        /// </summary>
        public DateTime? ScheduleTime { get; set; }

        /// <summary>
        /// 通知類型（範圍型態）
        /// </summary>
        public NotificationChannelType NotificationChannelType { get; set; }

        /// <summary>
        /// 下次執行時間
        /// </summary>
        public DateTime? NextRunAtUtc { get; set; }

        /// <summary>
        /// 建立時間
        /// </summary>
        public DateTime? CreateAtUtc { get; set; }

        /// <summary>
        /// 更新時間
        /// </summary>
        public DateTime? UpdateAtUtc { get; set; }

        /// <summary>
        /// 取消時間
        /// </summary>
        public DateTime? CancelledAtUtc { get; set; }
    }
}
