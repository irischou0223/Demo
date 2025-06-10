using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/registration")]
    public class RegistrationController : ControllerBase
    {
        private readonly RegistrationService _service;
        private readonly ILogger<RegistrationController> _logger;

        public RegistrationController(RegistrationService service, ILogger<RegistrationController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// 裝置註冊（若存在會自動切換狀態並新增新裝置）
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest req)
        {
            // 1. 基本輸入驗證
            if (string.IsNullOrWhiteSpace(req.DeviceId) ||
                string.IsNullOrWhiteSpace(req.FirebaseProjectId) ||
                string.IsNullOrWhiteSpace(req.AppVersion))
            {
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(req.DeviceId)) errors.Add("DeviceId 為必填。");
                if (string.IsNullOrWhiteSpace(req.FirebaseProjectId)) errors.Add("FirebaseProjectId 為必填。");
                if (string.IsNullOrWhiteSpace(req.AppVersion)) errors.Add("AppVersion 為必填。");

                _logger.LogWarning("裝置註冊請求缺少必要欄位：{Errors}", string.Join("; ", errors));
                return BadRequest(new
                {
                    message = "缺少必要的欄位。",
                    errors = errors
                });
            }

            try
            {
                // 2. 呼叫服務層進行裝置註冊業務邏輯
                var (device, errorMessage) = await _service.RegisterDeviceAsync(req);

                // 3. 根據服務層結果處理回應
                if (device == null)
                {
                    _logger.LogWarning("裝置註冊失敗，服務層返回錯誤：{ErrorMessage}，DeviceId={DeviceId}", errorMessage, req.DeviceId);

                    if (errorMessage == "找不到對應的產品資訊。")
                    {
                        return NotFound(errorMessage);
                    }
                    return BadRequest(errorMessage);
                }
                _logger.LogInformation("裝置註冊成功！新裝置 DeviceInfoId={DeviceInfoId}, DeviceId={DeviceId}", device.DeviceInfoId, device.DeviceId);
                return Ok(device);
            }
            catch (Exception ex)
            {
                // 4. 捕獲並處理任何未預期的例外
                _logger.LogError(ex, "裝置註冊發生非預期例外！DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);
                return StatusCode(StatusCodes.Status500InternalServerError, "裝置註冊時發生非預期錯誤，請稍後再試。");
            }
        }
    }
}
