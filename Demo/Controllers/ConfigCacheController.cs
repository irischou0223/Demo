using Demo.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/config-cache")]
    // [Authorize(Roles = "Admin")]
    public class ConfigCacheController : ControllerBase
    {
        private readonly ConfigCacheService _configCache;

        public ConfigCacheController(ConfigCacheService configCache)
        {
            _configCache = configCache;
        }

        /// <summary>
        /// 取得指定產品的 NotificationConfig
        /// </summary>
        [HttpGet("notification-config")]
        public async Task<IActionResult> GetNotificationConfig([FromQuery] Guid productInfoId)
        {
            if (productInfoId == Guid.Empty)
                return BadRequest("productInfoId is required.");

            var result = await _configCache.GetNotificationConfigAsync(productInfoId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效
        /// </summary>
        [HttpPost("invalidate-notification-config")]
        public async Task<IActionResult> InvalidateNotificationConfig([FromQuery] Guid productInfoId)
        {
            if (productInfoId == Guid.Empty)
                return BadRequest("productInfoId is required.");

            await _configCache.InvalidateNotificationConfigCacheAsync(productInfoId);
            return Ok("Cache invalidated.");
        }

        /// <summary>
        /// 查詢指定產品的 NotificationConfig 快取內容（僅查快取，不查DB）
        /// </summary>
        [HttpGet("peek-notification-config")]
        public async Task<IActionResult> PeekNotificationConfig([FromQuery] Guid productInfoId)
        {
            if (productInfoId == Guid.Empty)
                return BadRequest("productInfoId is required.");

            var cacheInfo = await _configCache.PeekCacheAsync(productInfoId);
            if (cacheInfo == null)
                return NotFound("No cache found.");
            return Ok(cacheInfo);
        }

        /// <summary>
        /// 批次清除多個產品的 NotificationConfig 快取
        /// </summary>
        [HttpPost("batch-invalidate-notification-config")]
        public async Task<IActionResult> BatchInvalidateNotificationConfig([FromBody] List<Guid> productInfoIds)
        {
            if (productInfoIds == null || productInfoIds.Count == 0)
                return BadRequest("productInfoIds is required.");

            foreach (var id in productInfoIds)
            {
                if (id != Guid.Empty)
                    await _configCache.InvalidateNotificationConfigCacheAsync(id);
            }
            return Ok("Batch cache invalidated.");
        }

        /// <summary>
        /// 清除全部 NotificationConfig Redis 快取
        /// </summary>
        [HttpPost("invalidate-all-notification-config")]
        public async Task<IActionResult> InvalidateAllNotificationConfig()
        {
            await _configCache.InvalidateAllNotificationConfigCacheAsync();
            return Ok("All notification config cache invalidated.");
        }

        /// <summary>
        /// 查詢 Redis 目前 NotificationConfig 快取數量
        /// </summary>
        [HttpGet("notification-config-cache-count")]
        public async Task<IActionResult> GetNotificationConfigCacheCount()
        {
            var count = await _configCache.GetNotificationConfigCacheCountAsync();
            return Ok(new { count });
        }
    }
}
