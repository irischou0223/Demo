using Demo.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/config-cache")]
    [Authorize(Policy = "AdminPolicy")] // 啟用授權
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
        /// 先嘗試從快取中讀取配置。如果快取中沒有，則會從資料庫或其他來源載入並存入快取。
        /// </summary>
        [HttpGet("notification-config")]
        public async Task<IActionResult> GetNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("Received request to get notification config for ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("Invalid ProductInfoId received: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                var result = await _configCache.GetNotificationConfigAsync(productInfoId);
                if (result == null)
                {
                    _logger.LogInformation("Notification config not found for ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound();
                }
                _logger.LogInformation("Successfully retrieved notification config for ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification config for ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving notification config.");
            }
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效
        /// 從快取中移除特定 productInfoId 的通知配置，確保下次查詢時會重新載入最新的資料。
        /// </summary>
        [HttpPost("invalidate-notification-config")]
        public async Task<IActionResult> InvalidateNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("Received request to invalidate notification config for ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("Invalid ProductInfoId received for invalidation: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                await _configCache.InvalidateNotificationConfigCacheAsync(productInfoId);
                _logger.LogInformation("Successfully invalidated cache for ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(new { message = "Cache invalidated." }); // 統一 JSON 響應格式
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while invalidating cache.");
            }
        }

        /// <summary>
        /// 查詢指定產品的 NotificationConfig 快取內容（僅查快取，不查DB）
        /// 用於調試或監控目的，它只會檢查快取中是否存在該配置，而不會觸發從資料庫載入。
        /// </summary>
        [HttpGet("peek-notification-config")]
        public async Task<IActionResult> PeekNotificationConfig([FromQuery] Guid productInfoId)
        {
            _logger.LogInformation("Received request to peek notification config for ProductInfoId: {ProductInfoId}", productInfoId);

            if (productInfoId == Guid.Empty)
            {
                _logger.LogWarning("Invalid ProductInfoId received for peeking: {ProductInfoId} (Guid.Empty)", productInfoId);
                return BadRequest("productInfoId is required and cannot be empty.");
            }

            try
            {
                var cacheInfo = await _configCache.PeekCacheAsync(productInfoId);
                if (cacheInfo == null)
                {
                    _logger.LogInformation("No cache found for ProductInfoId: {ProductInfoId}", productInfoId);
                    return NotFound(new { message = "No cache found." }); // 統一 JSON 響應格式
                }
                _logger.LogInformation("Successfully peeked cache for ProductInfoId: {ProductInfoId}", productInfoId);
                return Ok(cacheInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error peeking cache for ProductInfoId: {ProductInfoId}", productInfoId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while peeking cache.");
            }
        }

        /// <summary>
        /// 批次清除多個產品的 NotificationConfig 快取
        /// 允許一次性使多個產品的通知配置快取失效。
        /// </summary>
        [HttpPost("batch-invalidate-notification-config")]
        public async Task<IActionResult> BatchInvalidateNotificationConfig([FromBody] List<Guid> productInfoIds)
        {
            _logger.LogInformation("Received request to batch invalidate notification config for {Count} products.", productInfoIds?.Count ?? 0);

            if (productInfoIds == null || !productInfoIds.Any())
            {
                _logger.LogWarning("Batch invalidation request received with no productInfoIds.");
                return BadRequest("productInfoIds is required and cannot be empty.");
            }

            // 檢查列表中是否有無效的 Guid
            if (productInfoIds.Any(id => id == Guid.Empty))
            {
                _logger.LogWarning("Batch invalidation request contains invalid ProductInfoIds (Guid.Empty).");
                return BadRequest("Some productInfoIds in the list are invalid (Guid.Empty).");
            }

            try
            {
                foreach (var id in productInfoIds)
                {
                    // 注意：這裡如果某個單獨的失效操作失敗，整個批次操作仍然會繼續，
                    // 且最終返回成功。如果需要更嚴格的 "all or nothing" 語義，
                    // 則需要收集每個操作的結果並統一判斷。
                    await _configCache.InvalidateNotificationConfigCacheAsync(id);
                    _logger.LogDebug("Invalidated cache for individual ProductInfoId: {ProductInfoId}", id);
                }
                _logger.LogInformation("Successfully batch invalidated cache for {Count} products.", productInfoIds.Count);
                return Ok(new { message = "Batch cache invalidated." }); // 統一 JSON 響應格式
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch invalidation of cache for {Count} products.", productInfoIds.Count);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during batch cache invalidation.");
            }
        }

        /// <summary>
        /// 清除全部 NotificationConfig Redis 快取
        /// 清除所有與 NotificationConfig 相關的快取項目。通常用於系統維護或強制全面刷新快取。
        /// </summary>
        [HttpPost("invalidate-all-notification-config")]
        public async Task<IActionResult> InvalidateAllNotificationConfig()
        {
            _logger.LogWarning("Received request to invalidate ALL notification config cache."); // 警告級日誌

            try
            {
                await _configCache.InvalidateAllNotificationConfigCacheAsync();
                _logger.LogInformation("Successfully invalidated all notification config cache.");
                return Ok(new { message = "All notification config cache invalidated." }); // 統一 JSON 響應格式
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all notification config cache.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while invalidating all cache.");
            }
        }

        /// <summary>
        /// 查詢 Redis 目前 NotificationConfig 快取數量
        /// 用於監控和了解快取中當前有多少個 NotificationConfig 項目。
        /// </summary>
        [HttpGet("notification-config-cache-count")]
        public async Task<IActionResult> GetNotificationConfigCacheCount()
        {
            _logger.LogInformation("Received request to get notification config cache count.");

            try
            {
                var count = await _configCache.GetNotificationConfigCacheCountAsync();
                _logger.LogInformation("Retrieved notification config cache count: {Count}", count);
                return Ok(new { count = count }); // 返回包含計數的匿名物件
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification config cache count.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting cache count.");
            }
        }
    }
}
