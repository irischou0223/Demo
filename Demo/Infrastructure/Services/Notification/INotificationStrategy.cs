using Demo.Data.Entities;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// 推播策略介面，各平台推播均需實作此介面
    /// </summary>
    public interface INotificationStrategy
    {
        /// <summary>
        /// 發送通知（可支援多裝置）
        /// </summary>
        /// <param name="devices">目標裝置清單</param>
        /// <param name="title">標題</param>
        /// <param name="message">訊息內容</param>
        /// <param name="data">附加資料</param>
        Task SendAsync(List<DeviceInfo> devices, string title, string body, Dictionary<string, string> data, NotificationMsgTemplate template);
    }
}
