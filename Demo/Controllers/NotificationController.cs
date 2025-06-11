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
            _logger.LogInformation("接收到通知請求。來源類型: {Source}, 裝置數量: {DeviceCount}", request.Source, request.DeviceInfoIds?.Count ?? 0);

            // 1. 輸入驗證
            if (request == null)
            {
                _logger.LogWarning("通知請求為空 (null)。");
                return BadRequest(new { message = "通知請求內容不可為空。" });
            }

            var errors = new List<string>();

            // 判斷是否為排程通知（依 NotificationMsgTemplateId 有無）
            bool isScheduledNotification = request.NotificationMsgTemplateId.HasValue && request.NotificationMsgTemplateId != Guid.Empty;

            if (isScheduledNotification)
            {
                // 排程通知
                if (request.NotificationMsgTemplateId == Guid.Empty)
                {
                    errors.Add("排程通知必須提供有效的 NotificationMsgTemplateId。");
                }
                // 若未同時提供模板ID和自訂內容，則為不完整
                if (!request.NotificationMsgTemplateId.HasValue && string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
                {
                    errors.Add("排程通知必須提供 NotificationMsgTemplateId 或自訂 Title/Body。");
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
                errors.Add("必須提供目標裝置 DeviceIds 清單或 NotificationGroup。");
            }

            // 語系驗證
            if (string.IsNullOrWhiteSpace(request.Lang))
            {
                errors.Add("語系 (Lang) 為必填。");
            }

            if (errors.Any())
            {
                _logger.LogWarning("通知請求缺少必要欄位或不符合業務規則。錯誤: {Errors}", string.Join("; ", errors));
                return BadRequest(new { message = "請求內容無效。", errors = errors });
            }

            // 2. 判斷是否進 queue（裝置數量 > 1000 時進行 queue 處理）
            var useQueue = request.DeviceInfoIds?.Count > 1000;

            try
            {
                _logger.LogInformation("[ NotificationAPI ] 開始呼叫 NotificationService。來源: {Source}，useQueue: {UseQueue}", request.Source, useQueue);

                // 3. 呼叫 NotificationService 處理推播
                var result = await _notificationService.NotifyAsync(request, request.Source, useQueue);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("[ NotificationAPI ] 推播成功。訊息: {ResultMessage}", result.Message);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("[ NotificationAPI ] 推播失敗。錯誤訊息: {ErrorMessage}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ NotificationAPI ] 處理通知請求發生例外。來源: {Source}, DeviceIds數: {DeviceCount}", request.Source, request.DeviceInfoIds?.Count ?? 0);
                return StatusCode(StatusCodes.Status500InternalServerError, "處理通知時發生非預期錯誤，請稍後再試。");
            }
        }
    }
}
