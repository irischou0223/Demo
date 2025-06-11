using Demo.Enum;
using Demo.Models.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 推播佇列服務（NotificationQueueService）
    /// ---
    /// 管理推播任務的 Queue（採用 Redis List），提供入列、出列、查詢長度等操作。
    /// 流程說明：
    /// 1. EnqueueAsync：將推播請求加入 Queue，供後台排程/Worker 處理。
    /// 2. DequeueAsync：從 Queue 取出一筆任務（FIFO），給 Worker 消化。
    /// 3. GetQueueLengthAsync：查詢目前待處理的推播數量（可用於監控）。
    /// </summary>
    public class NotificationQueueService
    {
        private readonly IDatabase _redisDb;
        private readonly ILogger<NotificationQueueService> _logger;
        private const string QueueKey = "notification:tasks";

        public NotificationQueueService(IConnectionMultiplexer redis, ILogger<NotificationQueueService> logger)
        {
            _redisDb = redis.GetDatabase();
            _logger = logger;
        }

        /// <summary>
        /// 將推播請求加入 Queue
        /// </summary>
        /// <param name="request">推播請求 DTO</param>
        /// <param name="source">推播來源類型</param>
        public async Task EnqueueAsync(NotificationRequestDto request, NotificationSourceType source)
        {
            _logger.LogInformation("[ NotificationQueueService ] EnqueueAsync 開始，來源: {Source}", source);

            var item = new QueueItemDto
            {
                Request = request,
                Source = source,
                EnqueueTime = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(item);
            await _redisDb.ListRightPushAsync(QueueKey, json);

            _logger.LogInformation("[ NotificationQueueService ] EnqueueAsync 結束，已加入 Queue, 來源: {Source}", source);
        }

        /// <summary>
        /// 從Queue取出一筆推播請求（FIFO）
        /// </summary>
        public async Task<QueueItemDto> DequeueAsync()
        {
            _logger.LogInformation("[ NotificationQueueService ] DequeueAsync 開始");

            var value = await _redisDb.ListLeftPopAsync(QueueKey);
            if (value.IsNullOrEmpty)
            {
                _logger.LogInformation("[ NotificationQueueService ] DequeueAsync 結束，Queue 為空");
                return null;
            }

            var item = JsonSerializer.Deserialize<QueueItemDto>(value);
            _logger.LogInformation("[ NotificationQueueService ] DequeueAsync 結束，已取出一筆任務，EnqueueTime={EnqueueTime}", item.EnqueueTime);
            return item;
        }

        /// <summary>
        /// 取得目前Queue裡的待處理數量
        /// </summary>
        public async Task<long> GetQueueLengthAsync()
        {
            var length = await _redisDb.ListLengthAsync(QueueKey);
            _logger.LogInformation("[ NotificationQueueService ] GetQueueLengthAsync，Queue 長度: {Length}", length);
            return length;
        }
    }
}
