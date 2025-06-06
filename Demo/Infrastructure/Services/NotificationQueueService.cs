using Demo.Enum;
using Demo.Models.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace Demo.Infrastructure.Services
{
    public class NotificationQueueItem
    {
        public NotificationRequestDto Request { get; set; }
        public NotificationSourceType Source { get; set; }
        public DateTime EnqueueTime { get; set; }
    }

    public class NotificationQueueService
    {
        private readonly IDatabase _redisDb;
        private const string QueueKey = "notification:tasks";

        public NotificationQueueService(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        /// <summary>
        /// 將推播請求加入Queue
        /// </summary>
        public async Task EnqueueAsync(NotificationRequestDto request, NotificationSourceType source)
        {
            var item = new NotificationQueueItem
            {
                Request = request,
                Source = source,
                EnqueueTime = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(item);
            await _redisDb.ListRightPushAsync(QueueKey, json);
        }

        /// <summary>
        /// 從Queue取出一筆推播請求（FIFO）
        /// </summary>
        public async Task<NotificationQueueItem> DequeueAsync()
        {
            var value = await _redisDb.ListLeftPopAsync(QueueKey);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<NotificationQueueItem>(value);
        }

        /// <summary>
        /// 取得目前Queue裡的待處理數量
        /// </summary>
        public async Task<long> GetQueueLengthAsync()
        {
            return await _redisDb.ListLengthAsync(QueueKey);
        }
    }
}
