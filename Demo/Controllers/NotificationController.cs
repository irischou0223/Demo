using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    /// <summary>
    /// 通知 API Controller
    /// ---
    /// 主要負責外部通知請求的統一入口，驗證請求參數後分流至 NotificationService 執行，支援自動判斷是否進 queue。
    /// 流程說明：
    /// 1. 接收通知請求（/api/Notification/notify）
    /// 2. 進行參數完整性與業務規則驗證
    /// 3. 判斷通知型別與推播條件（如是否須進 queue）
    /// 4. 呼叫 NotificationService 完成推播
    /// 5. 回傳推播結果或錯誤訊息，並完整記錄 Log
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(NotificationService notificationService, ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// 通知主入口，支援自動判斷是否進 queue，來源由外部指定
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("notify")]
        public async Task<IActionResult> Notify([FromBody] NotificationRequestDto request)
        {
            _logger.LogInformation("[Notification] Notification request received. Source: {Source}, DeviceCount: {DeviceCount}, Lang: {Lang}, NotificationMsgTemplateId: {TemplateId}, NotificationGroup: {NotificationGroup}",
                request?.Source, request?.DeviceInfoIds?.Count ?? 0, request?.Lang, request?.NotificationMsgTemplateId, request?.NotificationGroup);


            // 1. 輸入驗證
            if (request == null)
            {
                _logger.LogWarning("[Notification] Notification request body is null.");
                return BadRequest(new { message = "Notification request body cannot be null." });
            }

            var errors = new List<string>();

            // 判斷是否為排程通知（依 NotificationMsgTemplateId 有無）
            bool isScheduledNotification = request.NotificationMsgTemplateId.HasValue && request.NotificationMsgTemplateId != Guid.Empty;

            if (isScheduledNotification)
            {
                // 排程通知
                if (request.NotificationMsgTemplateId == Guid.Empty)
                {
                    errors.Add("A valid NotificationMsgTemplateId is required for scheduled notification.");
                }
                // 若未同時提供模板ID和自訂內容，則為不完整
                if (!request.NotificationMsgTemplateId.HasValue && string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
                {
                    errors.Add("Scheduled notification must provide NotificationMsgTemplateId or custom Title/Body.");
                }
            }
            else
            {
                // 一般通知
                //if (string.IsNullOrWhiteSpace(request.Code))
                //{
                //    errors.Add("一般通知必須提供通知代碼 (Code)。");
                //}
            }

            // 通用條件：DeviceIds 或 NotificationGroup 必須擇一且不可同時為空
            if ((request.DeviceInfoIds == null || !request.DeviceInfoIds.Any()) && string.IsNullOrWhiteSpace(request.NotificationGroup))
            {
                errors.Add("Either DeviceInfoIds list or NotificationGroup is required.");
            }

            // 語系驗證
            if (string.IsNullOrWhiteSpace(request.Lang))
            {
                errors.Add("Lang is required.");
            }

            if (errors.Any())
            {
                _logger.LogWarning("[Notification] Request validation failed. Errors: {Errors}. Source: {Source}, DeviceCount: {DeviceCount}", string.Join("; ", errors), request.Source, request.DeviceInfoIds?.Count ?? 0);
                return BadRequest(new { message = "Invalid request content.", errors });
            }

            // 2. 判斷是否進 queue（裝置數量 > 1000 時進行 queue 處理）
            var useQueue = request.DeviceInfoIds?.Count > 1000;

            try
            {
                _logger.LogInformation("[Notification] Calling NotificationService. Source: {Source}, UseQueue: {UseQueue}", request.Source, useQueue);

                // 3. 呼叫 NotificationService 處理推播
                var result = await _notificationService.NotifyAsync(request, useQueue);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("[Notification] Notification sent successfully. Message: {Message}, Source: {Source}", result.Message, request.Source);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("[Notification] Notification send failed. Message: {Message}, Source: {Source}", result.Message, request.Source);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Notification] Exception occurred while processing notification. Source: {Source}, DeviceCount: {DeviceCount}", request.Source, request.DeviceInfoIds?.Count ?? 0);
                return StatusCode(StatusCodes.Status500InternalServerError, "處理通知時發生非預期錯誤，請稍後再試。");
            }
        }
    }
}
