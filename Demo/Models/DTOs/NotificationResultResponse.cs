namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播發送結果回應
    /// </summary>
    public class NotificationResultResponse
    {
        public bool IsSuccess { get; set; }
        /// <summary>
        /// 失敗裝置與Token對應 (DeviceId, Token)
        /// </summary>
        public Dictionary<string, string> FailedDeviceTokenMap { get; set; } = new(); 
        public string Message { get; set; }
    }
}
