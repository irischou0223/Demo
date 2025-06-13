using Demo.Enum;
using Demo.Models.DTOs;
using Serilog;
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
        public async Task EnqueueAsync(NotificationRequestDto request)
        {
            _logger.LogInformation("Enqueue notification request. Source={Source}", request.Source);

            var item = new QueueItemDto
            {
                Request = request,
                EnqueueTime = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(item);

            // 記錄 request 的關鍵資訊
            _logger.LogInformation(
                "Notification enqueued. Source={Source}, DeviceInfoIds={DeviceInfoIds}, Title={Title}, EnqueueTime={EnqueueTime}, QueueKey={QueueKey}",
                request.Source, request.DeviceInfoIds, request.Title, item.EnqueueTime, QueueKey);

            await _redisDb.ListRightPushAsync(QueueKey, json);

            // 監控 queue 長度
            var length = await _redisDb.ListLengthAsync(QueueKey);
            _logger.LogInformation("Enqueue complete. Current queue length={Length}", length);
        }

        /// <summary>
        /// 從Queue取出一筆推播請求（FIFO）
        /// </summary>
        public async Task<QueueItemDto> DequeueAsync()
        {
            _logger.LogInformation("Dequeue notification request started.");

            var value = await _redisDb.ListLeftPopAsync(QueueKey);
            if (value.IsNullOrEmpty)
            {
                _logger.LogInformation("Dequeue complete. Queue is empty.");
                return null;
            }

            try
            {
                var item = JsonSerializer.Deserialize<QueueItemDto>(value);
                if (item == null)
                {
                    _logger.LogWarning("Deserialization failed for dequeued item. Value={Value}", value);
                    return null;
                }
                var delaySeconds = (DateTime.UtcNow - item.EnqueueTime).TotalSeconds;
                _logger.LogInformation(
                    "Notification dequeued. Source={Source}, DeviceInfoIds={DeviceInfoIds}, Title={Title}, EnqueueTime={EnqueueTime}, DequeueTime={DequeueTime}, DelaySeconds={DelaySeconds}, QueueKey={QueueKey}",
                    item.Request?.Source, item.Request?.DeviceInfoIds, item.Request?.Title, item.EnqueueTime, DateTime.UtcNow, delaySeconds, QueueKey);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization failed. Dropping value: {Value}", value);
                return null;
            }
        }

        /// <summary>
        /// 取得目前Queue裡的待處理數量
        /// </summary>
        public async Task<long> GetQueueLengthAsync()
        {
            var length = await _redisDb.ListLengthAsync(QueueKey);
            _logger.LogInformation("GetQueueLength executed. Current queue length={Length}", length);
            return length;
        }
    }
}
