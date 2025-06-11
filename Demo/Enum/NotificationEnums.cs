using System.ComponentModel;

namespace Demo.Enum
{

    /// <summary>
    /// 通知通道類型（推播渠道）
    /// ---
    /// 用於標示通知發送方式（App/Web/Email/Line…）
    /// </summary>
    public enum NotificationChannelType
    {
        /// <summary>
        /// 行動應用程式通知 (e.g., FCM)
        /// </summary>
        [Description("APP")]
        App = 1,

        /// <summary>
        /// 網頁推播通知
        /// </summary>
        [Description("Web")]
        Web = 2,

        /// <summary>
        /// 電子郵件
        /// </summary>
        [Description("Email")]
        Email = 3,

        /// <summary>
        /// Line 訊息
        /// </summary>
        [Description("Line")]
        Line = 4
    }

    /// <summary>
    /// 通知的來源類型
    /// ---
    /// 標示通知請求來源（供審計、權限、日誌等用途使用）
    /// </summary>
    public enum NotificationSourceType
    {
        /// <summary>
        /// 後端系統內部觸發，例如手動發送、管理員操作等。
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

    /// <summary>
    /// 通知目標範圍
    /// ---
    /// 決定推播的對象範圍（單一裝置/群組/全部）
    /// </summary>
    public enum NotificationScopeType
    {
        /// <summary>
        /// 單一裝置
        /// </summary>
        [Description("Single")]
        Single = 1,

        /// <summary>
        /// 群組
        /// </summary>
        [Description("Group")]
        Group = 2,

        /// <summary>
        /// 全部裝置
        /// </summary>
        [Description("All")]
        All = 3
    }

    /// <summary>
    /// 排程頻率類型
    /// ---
    /// 控制排程任務的執行週期
    /// </summary>
    public enum ScheduleFrequencyType
    {
        /// <summary>
        /// 立即執行
        /// </summary>
        [Description("Immediate")]
        Immediate = 0,

        /// <summary>
        /// 每日執行
        /// </summary>
        [Description("Daily")]
        Daily = 1,

        /// <summary>
        /// 每月執行
        /// </summary>
        [Description("Monthly")]
        Monthly = 2,

        /// <summary>
        /// 每年執行
        /// </summary>
        [Description("Yearly")]
        Yearly = 3,

        /// <summary>
        /// 自訂（保留擴充用）
        /// </summary>
        [Description("Custom")]
        Custom = 9
    }
}
