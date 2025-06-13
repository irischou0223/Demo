using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    /// <summary>
    /// 裝置註冊 API Controller
    ///  ---
    /// 流程說明：
    /// 1. 驗證輸入參數（DeviceId, FirebaseProjectId, AppVersion）。
    /// 2. 呼叫 RegistrationService 處理註冊邏輯（裝置唯一性管理）。
    ///    - 若裝置不存在，直接新增並設為啟用。
    ///    - 若裝置存在，將舊裝置設為不啟用後再新增新裝置（啟用）。
    /// 3. 根據註冊結果回應 OK 或錯誤訊息。
    /// 4. 例外處理與詳細日誌記錄。
    /// </summary>
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
        /// 註冊裝置 API（App/Web/iOS/Android通用）
        /// </summary>
        /// <param name="req">註冊所需資訊</param>
        /// <returns>註冊成功則回傳裝置資訊，否則回傳錯誤訊息</returns>
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto req)
        {
            _logger.LogInformation("[Registration] Registration request received. DeviceId: {DeviceId}, FirebaseProjectId: {FirebaseProjectId}, AppVersion: {AppVersion}, UserAccount: {UserAccount}",
                req?.DeviceId, req?.FirebaseProjectId, req?.AppVersion, req?.UserAccount);

            var errors = ValidateRegisterRequest(req);
            if (errors.Count > 0)
            {
                _logger.LogWarning("[Registration] Registration request validation failed. Errors: {Errors}, DeviceId: {DeviceId}", string.Join("; ", errors), req?.DeviceId);
                return BadRequest(new { message = "Missing required fields.", errors });
            }

            try
            {
                var (device, errorMessage) = await _service.RegisterDeviceAsync(req);
                if (device == null)
                {
                    _logger.LogWarning("[Registration] Registration failed. Error: {ErrorMessage}, DeviceId: {DeviceId}", errorMessage, req.DeviceId);
                    if (errorMessage == "No matching product info found.")
                        return NotFound(errorMessage);
                    return BadRequest(errorMessage);
                }
                _logger.LogInformation("[Registration] Registration succeeded. DeviceInfoId: {DeviceInfoId}, DeviceId: {DeviceId}", device.DeviceInfoId, device.DeviceId);
                return Ok(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Registration] Exception occurred during registration. DeviceId: {DeviceId}, UserAccount: {UserAccount}", req?.DeviceId, req?.UserAccount);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred during registration." });
            }
        }

        /// <summary>
        /// 驗證註冊請求內容，將錯誤訊息集中管理。
        /// </summary>
        private static List<string> ValidateRegisterRequest(RegisterRequestDto req)
        {
            var errors = new List<string>();
            if (req == null)
            {
                errors.Add("Request body cannot be null.");
                return errors;
            }
            if (string.IsNullOrWhiteSpace(req.DeviceId)) errors.Add("DeviceId is required.");
            if (string.IsNullOrWhiteSpace(req.FirebaseProjectId)) errors.Add("FirebaseProjectId is required.");
            if (string.IsNullOrWhiteSpace(req.AppVersion)) errors.Add("AppVersion is required.");
            // 其他欄位可依需求擴充
            return errors;
        }
    }
}
