using Demo.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    /// <summary>
    /// NotificationActionConfig 快取管理 API Controller
    /// ---
    /// 提供管理 NotificationActionConfig 快取（查詢、失效、數量監控等）之 REST 介面。
    /// 流程說明：
    /// 1. 支援快取查詢、失效、批次失效、全清除、快取數量查詢等操作
    /// 2. 操作皆詳盡記錄日誌，權限需 AdminPolicy
    /// 3. 例外皆統一格式回應
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
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
        /// 取得指定產品的 NotificationActionConfig
        /// 先查快取，未命中則自資料庫載入。
        /// </summary>
        [HttpGet("get-notification-action-config")]
        public async Task<IActionResult> GetNotificationActionConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[Cache] Begin querying notification action config. ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[Cache] Invalid ProductInfoId received (Guid.Empty). ProductInfoId: {ProductInfoId}", productInfoId);
                return BadRequest(new { message = "productInfoId is required and cannot be empty.", productInfoId });
            }

            try
            {
                var result = await _configCache.GetNotificationActionConfigAsync(productInfoId);
                if (result == null)
                {
                    _logger.LogInformation("[Cache] No data found for product. ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound(new { message = "No cache found." });
                }
                _logger.LogInformation("[Cache] Successfully retrieved notification action config. ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception occurred while querying notification action config. ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while querying notification action config.", productInfoId });
            }
        }

        /// <summary>
        /// 使指定產品的 NotificationActionConfig 快取失效
        /// </summary>
        [HttpPost("invalidate-notification-action-config")]
        public async Task<IActionResult> InvalidateNotificationActionConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[Cache] Invalidate cache request received. ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[Cache] Invalid ProductInfoId received for invalidation (Guid.Empty). ProductInfoId: {ProductInfoId}", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                await _configCache.InvalidateNotificationConfigActionCacheAsync(productInfoId);
                _logger.LogInformation("[Cache] Cache invalidated successfully. ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(new { message = "Cache invalidated.", productInfoId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception occurred during cache invalidation. ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while invalidating cache.", productInfoId });
            }
        }

        /// <summary>
        /// 查詢指定產品的 NotificationActionConfig 快取內容（僅查快取）
        /// </summary>
        [HttpGet("peek-notification-action-config")]
        public async Task<IActionResult> PeekNotificationActionConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("[Cache] Peek cache requested. ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("[Cache] Invalid ProductInfoId received for peek (Guid.Empty). ProductInfoId: {ProductInfoId}", productInfoId);
                return BadRequest(new { message = "productInfoId is required and cannot be empty.", productInfoId });
            }

            try
            {
                var cacheInfo = await _configCache.PeekCacheAsync(productInfoId);
                if (cacheInfo == null)
                {
                    _logger.LogInformation("[Cache] No cache entry found. ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound(new { message = "No cache entry found.", productInfoId });
                }
                _logger.LogInformation("[Cache] Successfully peeked cache. ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(cacheInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception occurred while peeking cache. ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while peeking cache.", productInfoId });
            }
        }

        /// <summary>
        /// 批次清除多個產品的 NotificationActionConfig 快取
        /// </summary>
        [HttpPost("batch-invalidate-notification-action-config")]
        public async Task<IActionResult> BatchInvalidateNotificationActionConfig([FromBody] List<Guid> productInfoIds)
        {
            _logger.LogInformation("[Cache] Batch cache invalidation requested. ProductInfoIds: {ProductInfoIds}", productInfoIds);

            if (productInfoIds == null || !productInfoIds.Any())
            {
                _logger.LogWarning("[Cache] No ProductInfoIds provided for batch invalidation.");
                return BadRequest(new { message = "productInfoIds is required and cannot be empty.", productInfoIds });
            }
            if (productInfoIds.Any(id => id == Guid.Empty))
            {
                _logger.LogWarning("[Cache] Batch invalidation contains invalid ProductInfoId (Guid.Empty). ProductInfoIds: {ProductInfoIds}", productInfoIds);
                return BadRequest(new { message = "Some productInfoIds are invalid (Guid.Empty).", productInfoIds });
            }

            try
            {
                foreach (var id in productInfoIds)
                {
                    await _configCache.InvalidateNotificationConfigActionCacheAsync(id);
                    _logger.LogDebug("[Cache] Single product cache invalidated during batch. ProductInfoId: {ProductInfoId}", id);
                }
                _logger.LogInformation("[Cache] Batch cache invalidation completed. Count: {Count} ProductInfoIds: {ProductInfoIds}", productInfoIds.Count, productInfoIds);
                return Ok(new { message = "Batch cache invalidated.", productInfoIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception during batch cache invalidation. ProductInfoIds: {ProductInfoIds}", productInfoIds);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during batch cache invalidation.", productInfoIds });
            }
        }

        /// <summary>
        /// 清除全部 NotificationActionConfig Redis 快取
        /// </summary>
        [HttpPost("invalidate-all-notification-action-config")]
        public async Task<IActionResult> InvalidateAllNotificationActionConfig()
        {
            _logger.LogWarning("[Cache] Full cache invalidation requested.");

            try
            {
                await _configCache.InvalidateAllNotificationActionConfigCacheAsync();
                _logger.LogInformation("[Cache] All cache entries invalidated successfully.");
                return Ok(new { message = "All notification action config caches have been invalidated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception occurred during full cache invalidation.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while invalidating all cache." });
            }
        }

        /// <summary>
        /// 查詢 Redis 目前 NotificationActionConfig 快取數量
        /// </summary>
        [HttpGet("get-notification-config-action-cache-count")]
        public async Task<IActionResult> GetNotificationConfigActionCacheCount()
        {
            _logger.LogInformation("[Cache] Cache count query requested.");

            try
            {
                var count = await _configCache.GetNotificationConfigActionCacheCountAsync();
                _logger.LogInformation("[Cache] Current cache entry count: {Count}", count);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cache] Exception occurred while querying cache count.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while querying cache count." });
            }
        }
    }
}
