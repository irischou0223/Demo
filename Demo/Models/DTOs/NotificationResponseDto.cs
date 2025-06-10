namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播結果回應 DTO
    /// </summary>
    public class NotificationResponseDto
    {
        /// <summary>
        /// 推播是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// 失敗裝置與Token對應（DeviceId, Token）
        /// </summary>
        public Dictionary<string, string> FailedDeviceTokenMap { get; set; } = new();
        /// <summary>
        /// 訊息說明
        /// </summary>
        public string Message { get; set; }
    }
}
