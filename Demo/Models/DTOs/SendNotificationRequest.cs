namespace Demo.Models.DTOs
{
    /// <summary>
    /// 推播發送請求DTO
    /// </summary>
    public class SendNotificationRequest
    {
        public List<string> DeviceIds { get; set; }
        public string NotificationType { get; set; } // ex: App/Web/Email/Line
        public string Title { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }
}
