namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播結果回應 DTO
    /// ---
    /// 用於通知 API 回傳推播結果與說明。
    /// </summary>
    public class NotificationResponseDto
    {
        /// <summary>
        /// 推播是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// 失敗裝置與 Token 對應（DeviceId, Token），可用於後續補發、除錯
        /// </summary>
        public Dictionary<string, string> FailedDeviceTokenMap { get; set; } = new();
        /// <summary>
        /// 訊息說明（如成功/失敗或詳細原因）
        /// </summary>
        public string Message { get; set; }
    }
}
