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
            if (string.IsNullOrWhiteSpace(req.DeviceId) ||
                string.IsNullOrWhiteSpace(req.FirebaseProjectId) ||
                string.IsNullOrWhiteSpace(req.AppVersion))
            {
                _logger.LogWarning("裝置註冊請求缺少必要欄位 DeviceId={DeviceId}, FirebaseProjectId={FirebaseProjectId}, AppVersion={AppVersion}",
                    req.DeviceId, req.FirebaseProjectId, req.AppVersion);
                return BadRequest("DeviceId、FirebaseProjectId、AppVersion 必填");
            }

            try
            {
                var device = await _service.RegisterDeviceAsync(req);
                if (device == null)
                {
                    _logger.LogWarning("裝置註冊失敗，無法建立 device DeviceId={DeviceId}", req.DeviceId);
                    return NotFound($"Product: {req.FirebaseProjectId} 不存在");
                }

                _logger.LogInformation("裝置註冊成功 DeviceInfoId={DeviceInfoId}, DeviceId={DeviceId}", device.DeviceInfoId, device.DeviceId);
                return Ok(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "裝置註冊發生例外 DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);
                return StatusCode(500, "裝置註冊時發生錯誤：" + ex.Message);
            }
        }
    }
}
