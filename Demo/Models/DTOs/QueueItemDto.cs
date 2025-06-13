using Demo.Enum;

namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播佇列項目 DTO（QueueItemDto）
    /// ---
    /// 代表進入推播 Queue 的一筆任務，記錄請求內容、來源與入列時間。
    /// </summary>
    public class QueueItemDto
    {
        /// <summary>
        /// 推播請求內容
        /// </summary>
        public NotificationRequestDto Request { get; set; }

        /// <summary>
        /// 進入佇列的 UTC 時間
        /// </summary>
        public DateTime EnqueueTime { get; set; }
    }
}
