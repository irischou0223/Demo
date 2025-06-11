using Demo.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    /// <summary>
    /// NotificationConfig 快取管理 API Controller
    /// ---
    /// 提供管理 NotificationConfig 快取（查詢、失效、數量監控等）之 REST 介面。
    /// 流程說明：
    /// 1. 支援快取查詢、失效、批次失效、全清除、快取數量查詢等操作
    /// 2. 操作皆詳盡記錄日誌，權限需 AdminPolicy
    /// 3. 例外皆統一格式回應
    /// </summary>
    [ApiController]
    [Route("api/config-cache")]
    [Authorize(Policy = "AdminPolicy")]
    public class ConfigCacheController : ControllerBase
    {
        private readonly ConfigCacheService _configCache;
        private readonly ILogger<ConfigCacheController> _logger;

        public ConfigCacheController(ConfigCacheService configCache, ILogger<ConfigCacheController> logger)
        {
            _configCache = configCache;
            _logger = logger;
        }

        /// <summary>
        /// 取得指定產品的 NotificationConfig
        /// 先查快取，未命中則自資料庫載入。
        /// </summary>
        [HttpGet("notification-config")]
        public async Task<IActionResult> GetNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[ ConfigCacheAPI ] 收到查詢通知配置請求，ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[ ConfigCacheAPI ] 查詢通知配置，收到無效 ProductInfoId: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                var result = await _configCache.GetNotificationConfigAsync(productInfoId);
                if (result == null)
                {
                    _logger.LogInformation("[ ConfigCacheAPI ] 查詢通知配置未找到，ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound();
                }
                _logger.LogInformation("[ ConfigCacheAPI ] 查詢通知配置成功，ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] 查詢通知配置發生例外，ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving notification config.");
            }
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效
        /// </summary>
        [HttpPost("invalidate-notification-config")]
        public async Task<IActionResult> InvalidateNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[ ConfigCacheAPI ] 收到失效通知配置快取請求，ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[ ConfigCacheAPI ] 失效快取請求收到無效 ProductInfoId: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                await _configCache.InvalidateNotificationConfigCacheAsync(productInfoId);
                _logger.LogInformation("[ ConfigCacheAPI ] 快取失效成功，ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(new { message = "Cache invalidated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] 失效快取時發生例外，ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while invalidating cache.");
            }
        }

        /// <summary>
        /// 查詢指定產品的 NotificationConfig 快取內容（僅查快取）
        /// </summary>
        [HttpGet("peek-notification-config")]
        public async Task<IActionResult> PeekNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[ ConfigCacheAPI ] 收到快取 peek 請求，ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[ ConfigCacheAPI ] peek 快取請求收到無效 ProductInfoId: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                var cacheInfo = await _configCache.PeekCacheAsync(productInfoId);
                if (cacheInfo == null)
                {
                    _logger.LogInformation("[ ConfigCacheAPI ] 快取中無資料，ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound(new { message = "No cache found." });
                }
                _logger.LogInformation("[ ConfigCacheAPI ] peek 快取成功，ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(cacheInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] peek 快取發生例外，ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while peeking cache.");
            }
        }

        /// <summary>
        /// 批次清除多個產品的 NotificationConfig 快取
        /// </summary>
        [HttpPost("batch-invalidate-notification-config")]
        public async Task<IActionResult> BatchInvalidateNotificationConfig([FromBody] List<Guid> productInfoIds)
        {
            _logger.LogInformation("[ ConfigCacheAPI ] 收到批次失效快取請求，產品數: {Count}", productInfoIds?.Count ?? 0);

            if (productInfoIds == null || !productInfoIds.Any())
            {
                _logger.LogWarning("[ ConfigCacheAPI ] 批次失效請求無 ProductInfoIds。");
                return BadRequest("productInfoIds is required and cannot be empty.");
            }
            if (productInfoIds.Any(id => id == Guid.Empty))
            {
                _logger.LogWarning("[ ConfigCacheAPI ] 批次失效請求含無效 ProductInfoId (Guid.Empty)。");
                return BadRequest("Some productInfoIds in the list are invalid (Guid.Empty).");
            }

            try
            {
                foreach (var id in productInfoIds)
                {
                    await _configCache.InvalidateNotificationConfigCacheAsync(id);
                    _logger.LogDebug("[ ConfigCacheAPI ] 已失效單一 ProductInfoId: {ProductInfoId}", id);
                }
                _logger.LogInformation("[ ConfigCacheAPI ] 批次失效快取成功，數量: {Count}", productInfoIds.Count);
                return Ok(new { message = "Batch cache invalidated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] 批次快取失效發生例外，數量: {Count}", productInfoIds.Count);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during batch cache invalidation.");
            }
        }

        /// <summary>
        /// 清除全部 NotificationConfig Redis 快取
        /// </summary>
        [HttpPost("invalidate-all-notification-config")]
        public async Task<IActionResult> InvalidateAllNotificationConfig()
        {
            _logger.LogWarning("[ ConfigCacheAPI ] 收到全清除 NotificationConfig 快取請求！");

            try
            {
                await _configCache.InvalidateAllNotificationConfigCacheAsync();
                _logger.LogInformation("[ ConfigCacheAPI ] 全部快取已清除。");
                return Ok(new { message = "All notification config cache invalidated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] 全清除快取發生例外。");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while invalidating all cache.");
            }
        }

        /// <summary>
        /// 查詢 Redis 目前 NotificationConfig 快取數量
        /// </summary>
        [HttpGet("notification-config-cache-count")]
        public async Task<IActionResult> GetNotificationConfigCacheCount()
        {
            _logger.LogInformation("[ ConfigCacheAPI ] 收到查詢快取數量請求。");

            try
            {
                var count = await _configCache.GetNotificationConfigCacheCountAsync();
                _logger.LogInformation("[ ConfigCacheAPI ] 目前快取數量: {Count}", count);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ConfigCacheAPI ] 查詢快取數量發生例外。");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting cache count.");
            }
        }
    }
}
