using System.ComponentModel;

namespace Demo.Enum
{
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
}
