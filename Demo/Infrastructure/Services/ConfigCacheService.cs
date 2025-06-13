using Demo.Data;
using Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 通知配置快取服務（ConfigCacheService）
    /// ---
    /// 支援多層快取架構：
    /// 1. MemoryCache（本機記憶體，最快）
    /// 2. Redis（分散式，跨機房/實例共用）
    /// 3. 資料庫（最終資料來源）
    ///
    /// 流程說明：
    /// - 讀取順序：Memory → Redis → DB，並於命中時補齊上層快取。
    /// - 寫入/失效：同時操作 Memory 與 Redis。
    /// - 管理 API 與維運皆經由本服務存取快取資料。
    public class ConfigCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDatabase _redisDb;
        private readonly DemoDbContext _db;
        private readonly ILogger<ConfigCacheService> _logger;

        // 快取存活時間
        private static readonly TimeSpan MemoryExpire = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RedisExpire = TimeSpan.FromMinutes(30);

        public ConfigCacheService(IMemoryCache memoryCache, IConnectionMultiplexer redis, DemoDbContext db, ILogger<ConfigCacheService> logger)
        {
            _memoryCache = memoryCache;
            _redisDb = redis.GetDatabase();
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 產生快取 Key（依 productInfoId 唯一）
        /// </summary>
        private string GetConfigCacheKey(Guid productInfoId) => $"NotificationActionConfig:{productInfoId}";

        /// <summary>
        /// 取得 NotificationActionConfig（多層快取：先 Memory，再 Redis，最後 DB 並補回快取）
        /// </summary>
        /// <param name="productInfoId">產品唯一識別</param>
        /// <returns>通知配置實體或 null</returns>
        public async Task<NotificationActionConfig?> GetNotificationActionConfigAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);

            // 1. MemoryCache（本機快取）
            if (_memoryCache.TryGetValue(key, out NotificationActionConfig config))
            {
                _logger.LogDebug("MemoryCache hit. ProductInfoId={ProductInfoId}", productInfoId);
                return config;
            }
            _logger.LogDebug("MemoryCache miss. ProductInfoId={ProductInfoId}", productInfoId);

            // 2. Redis 分散式快取
            var redisValue = await _redisDb.StringGetAsync(key);
            if (redisValue.HasValue)
            {
                config = System.Text.Json.JsonSerializer.Deserialize<NotificationActionConfig>(redisValue);
                if (config != null)
                {
                    _memoryCache.Set(key, config, MemoryExpire);
                    _logger.LogDebug("Redis cache hit and restored to MemoryCache. ProductInfoId={ProductInfoId}", productInfoId);
                }
                else
                {
                    _logger.LogWarning("Redis cache hit but deserialization failed. ProductInfoId={ProductInfoId}", productInfoId);
                }
                return config;
            }
            _logger.LogDebug("Redis cache miss. ProductInfoId={ProductInfoId}", productInfoId);

            // 3. 資料庫查詢
            config = await _db.NotificationActionConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.ProductInfoId == productInfoId);
            if (config != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(config);
                await _redisDb.StringSetAsync(key, json, RedisExpire);
                _memoryCache.Set(key, config, MemoryExpire);
                _logger.LogInformation("Database hit. ProductInfoId={ProductInfoId}. Cache updated.", productInfoId);
            }
            else
            {
                _logger.LogWarning("No data found in database. ProductInfoId={ProductInfoId}", productInfoId);
            }
            return config;
        }

        /// <summary>
        /// 僅查快取，不查資料庫（維運/監控用途）
        /// </summary>
        /// <param name="productInfoId">產品唯一識別</param>
        /// <returns>通知配置快取內容或 null</returns>
        public async Task<NotificationActionConfig?> PeekCacheAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);

            // 1. 先查 Memory
            if (_memoryCache.TryGetValue(key, out NotificationActionConfig config))
            {
                _logger.LogDebug("Peek MemoryCache hit. ProductInfoId={ProductInfoId}", productInfoId);
                return config;
            }
            _logger.LogDebug("Peek MemoryCache miss. ProductInfoId={ProductInfoId}", productInfoId);

            // 2. 查 Redis
            var redisValue = await _redisDb.StringGetAsync(key);
            if (redisValue.HasValue)
            {
                _logger.LogDebug("Peek Redis cache hit. ProductInfoId={ProductInfoId}", productInfoId);
                return System.Text.Json.JsonSerializer.Deserialize<NotificationActionConfig>(redisValue);
            }
            _logger.LogDebug("Peek Redis cache miss. ProductInfoId={ProductInfoId}", productInfoId);

            // 3. 不查 DB
            return null;
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效（Memory/Redis 均移除）
        /// </summary>
        /// <param name="productInfoId">產品唯一識別</param>
        public async Task InvalidateNotificationConfigActionCacheAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);
            _memoryCache.Remove(key);
            await _redisDb.KeyDeleteAsync(key);
            _logger.LogInformation("Cache invalidated for ProductInfoId={ProductInfoId}", productInfoId);
        }

        /// <summary>
        /// 清除所有 NotificationConfig 快取（僅 Redis，可配合維運操作）
        /// </summary>
        public async Task InvalidateAllNotificationActionConfigCacheAsync()
        {
            var endpoints = _redisDb.Multiplexer.GetEndPoints();
            var server = _redisDb.Multiplexer.GetServer(endpoints.First());
            var keys = server.Keys(pattern: "NotificationActionConfig:*").ToArray();
            foreach (var key in keys)
            {
                await _redisDb.KeyDeleteAsync(key);
                _logger.LogDebug("Redis cache key deleted: {Key}", key);
            }
            _logger.LogWarning("All Redis NotificationActionConfig caches cleared.");
            // MemoryCache 無法全清，只能等過期或重啟
        }

        /// <summary>
        /// 查詢目前 Redis NotificationActionConfig 快取數量（監控用）
        /// </summary>
        /// <returns>Redis 中快取數量</returns>
        public Task<int> GetNotificationConfigActionCacheCountAsync()
        {
            var endpoints = _redisDb.Multiplexer.GetEndPoints();
            var server = _redisDb.Multiplexer.GetServer(endpoints.First());
            var keys = server.Keys(pattern: "NotificationActionConfig:*");
            var count = keys.Count();
            _logger.LogDebug("Current Redis NotificationActionConfig cache count: {Count}", count);
            return Task.FromResult(count);
        }
    }
}
