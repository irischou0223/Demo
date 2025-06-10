namespace Demo.Enum
{
    /// <summary>
    /// 通知的來源類型
    /// </summary>
    public enum NotificationSourceType
    {
        /// <summary>
        /// 後端系統內部觸發，例如手動發送、管理員操作等
        /// </summary>
        Backend,

        /// <summary>
        /// 外部系統或第三方服務觸發，透過 API 或其他整合方式傳遞。
        /// </summary>
        External,

        /// <summary>
        /// 排程任務 (Job) 或背景工作觸發，例如定時推送、批量處理等。
        /// </summary>
        Job
    }
}
