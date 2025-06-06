namespace Demo.Models.DTOs
{
    /// <summary>
    /// 排程新增請求DTO
    /// </summary>
    public class ScheduleJobRequest
    {
        public string NotificationType { get; set; }
        public List<string> DeviceIds { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public string Frequency { get; set; } // "立即", "每日", "每月", "每年", "指定時間"
        public DateTime ScheduleTime { get; set; }
    }
}
