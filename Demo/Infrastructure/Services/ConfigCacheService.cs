using Demo.Data;
using Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

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
        /// 清除指定產品的 NotificationActionConfig 快取（Memory & Redis）
        /// </summary>
        public async Task InvalidateNotificationConfigCacheAsync(Guid productInfoId)
        {
            string key = GetConfigCacheKey(productInfoId);
            _memoryCache.Remove(key);
            await _redisDb.KeyDeleteAsync(key);
        }
    }
}
