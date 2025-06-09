using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Demo.Data.Entities;
using Demo.Data;

namespace Demo.Infrastructure.Services
{
    public class ConfigCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDatabase _redisDb;
        private readonly DemoDbContext _db;

        private static readonly TimeSpan MemoryExpire = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RedisExpire = TimeSpan.FromMinutes(30);

        public ConfigCacheService(IMemoryCache memoryCache, IConnectionMultiplexer redis, DemoDbContext db)
        {
            _memoryCache = memoryCache;
            _redisDb = redis.GetDatabase();
            _db = db;
        }

        private string GetConfigCacheKey(Guid productInfoId) => $"NotificationActionConfig:{productInfoId}";

        /// <summary>
        /// 取得 NotificationActionConfig（先查 Memory，再查 Redis，最後查 DB 並回寫快取）
        /// </summary>
        public async Task<NotificationActionConfig?> GetNotificationConfigAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);

            // 1. MemoryCache
            if (_memoryCache.TryGetValue(key, out NotificationActionConfig config))
                return config;

            // 2. Redis
            var redisValue = await _redisDb.StringGetAsync(key);
            if (redisValue.HasValue)
            {
                config = System.Text.Json.JsonSerializer.Deserialize<NotificationActionConfig>(redisValue);
                // 寫入 MemoryCache
                if (config != null)
                    _memoryCache.Set(key, config, MemoryExpire);
                return config;
            }

            // 3. DB
            config = await _db.NotificationActionConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.ProductInfoId == productInfoId);
            if (config != null)
            {
                // 寫入 Redis & Memory
                var json = System.Text.Json.JsonSerializer.Serialize(config);
                await _redisDb.StringSetAsync(key, json, RedisExpire);
                _memoryCache.Set(key, config, MemoryExpire);
            }
            return config;
        }

        /// <summary>
        /// 僅查快取，不查DB（可用於維運查看快取內容）
        /// </summary>
        public async Task<NotificationActionConfig?> PeekCacheAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);

            // 先查 Memory
            if (_memoryCache.TryGetValue(key, out NotificationActionConfig config))
                return config;

            // 查 Redis
            var redisValue = await _redisDb.StringGetAsync(key);
            if (redisValue.HasValue)
                return System.Text.Json.JsonSerializer.Deserialize<NotificationActionConfig>(redisValue);

            return null;
        }

        /// <summary>
        /// 使指定產品的 NotificationConfig 快取失效
        /// </summary>
        public async Task InvalidateNotificationConfigCacheAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);
            _memoryCache.Remove(key);
            await _redisDb.KeyDeleteAsync(key);
        }

        /// <summary>
        /// 清除所有 NotificationConfig 快取（僅 Redis）
        /// </summary>
        public async Task InvalidateAllNotificationConfigCacheAsync()
        {
            var endpoints = _redisDb.Multiplexer.GetEndPoints();
            var server = _redisDb.Multiplexer.GetServer(endpoints.First());
            var keys = server.Keys(pattern: "NotificationActionConfig:*").ToArray();
            foreach (var key in keys)
            {
                await _redisDb.KeyDeleteAsync(key);
            }
            // MemoryCache 只能靠過期或重啟維護
        }

        /// <summary>
        /// 查詢 Redis 目前 NotificationConfig 快取數量
        /// </summary>
        public Task<int> GetNotificationConfigCacheCountAsync()
        {
            var endpoints = _redisDb.Multiplexer.GetEndPoints();
            var server = _redisDb.Multiplexer.GetServer(endpoints.First());
            var keys = server.Keys(pattern: "NotificationActionConfig:*");
            return Task.FromResult(keys.Count());
        }
    }
}
