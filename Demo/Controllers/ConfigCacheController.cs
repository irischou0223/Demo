using Demo.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/config-cache")]
    // [Authorize(Roles = "Admin")] // 可先註解，未來正式上線再加
    public class ConfigCacheController : ControllerBase
    {
        private readonly ConfigCacheService _configCache;

        public ConfigCacheController(ConfigCacheService configCache)
        {
            _configCache = configCache;
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效(重新從資料庫讀取)
        /// </summary>
        /// <param name="productInfoId">產品資訊ID</param>
        [HttpPost("invalidate-notification-config")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> InvalidateNotificationConfig([FromQuery] Guid productInfoId)
        {
            if (productInfoId == Guid.Empty)
                return BadRequest("productInfoId is required.");

            await _configCache.InvalidateNotificationConfigCacheAsync(productInfoId);
            return Ok("Cache invalidated.");
        }
    }
}
