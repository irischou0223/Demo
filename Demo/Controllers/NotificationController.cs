using Demo.Enum;
using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
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
            _logger.LogInformation("接收到通知請求。來源類型: {Source}, 裝置數量: {DeviceCount}", request.Source, request.DeviceIds?.Count ?? 0);

            // --- 輸入驗證 ---
            // 檢查 request 物件本身是否為 null
            if (request == null)
            {
                _logger.LogWarning("通知請求為空 (null)。");
                return BadRequest(new { message = "通知請求內容不可為空。" });
            }

            var errors = new List<string>();

            // 判斷是 "一般通知" 還是 "排程通知"，並進行不同的驗證
            // 假設如果 NotificationMsgTemplateId 有值，就是排程通知；否則就是一般通知
            bool isScheduledNotification = request.NotificationMsgTemplateId.HasValue && request.NotificationMsgTemplateId != Guid.Empty;

            if (isScheduledNotification)
            {
                // 排程通知專用驗證
                if (request.NotificationMsgTemplateId == Guid.Empty)
                {
                    errors.Add("排程通知必須提供有效的 NotificationMsgTemplateId。");
                }
                // 排程通知可以依賴模板，也可以提供自訂標題/內容
                // 如果同時提供模板ID和自訂內容，則自訂內容會覆蓋模板
                // 如果沒有提供模板ID，但也沒有提供Title或Body，則視為不完整
                if (!request.NotificationMsgTemplateId.HasValue && string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
                {
                    errors.Add("排程通知必須提供 NotificationMsgTemplateId 或自訂 Title/Body。");
                }
            }
            else
            {
                // 一般通知專用驗證
                if (string.IsNullOrWhiteSpace(request.Code))
                {
                    errors.Add("一般通知必須提供通知代碼 (Code)。");
                }
            }

            // --- 通用通知條件驗證 ---
            // DeviceIds 和 NotificationGroup 至少要有一個，且不能同時為空。
            if ((request.DeviceIds == null || !request.DeviceIds.Any()) && string.IsNullOrWhiteSpace(request.NotificationGroup))
            {
                errors.Add("必須提供目標裝置 DeviceIds 清單或 NotificationGroup。");
            }

            // Lang 通常會有預設值，但如果需要嚴格驗證其格式，可以在這裡添加
            if (string.IsNullOrWhiteSpace(request.Lang))
            {
                errors.Add("語系 (Lang) 為必填。");
            }

            if (errors.Any())
            {
                _logger.LogWarning("通知請求缺少必要欄位或不符合業務規則。錯誤: {Errors}", string.Join("; ", errors));
                return BadRequest(new { message = "請求內容無效。", errors = errors });
            }

            // 若裝置數大於 1000 則進 queue
            var useQueue = request.DeviceIds?.Count > 1000;

            try
            {
                var result = await _notificationService.NotifyAsync(request, request.Source, useQueue);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("通知服務呼叫成功。結果: {Result}", result.Message);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通知服務呼叫失敗。錯誤訊息: {ErrorMessage}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "處理通知請求時發生非預期錯誤。請求來源: {Source}, 裝置數量: {DeviceCount}", request.Source, request.DeviceIds?.Count ?? 0);
                return StatusCode(StatusCodes.Status500InternalServerError, "處理通知時發生非預期錯誤，請稍後再試。");

            }
        }
    }
}
