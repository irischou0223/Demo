using Demo.Data;
using Demo.Data.Entities;
using System.Collections.Concurrent;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 推播日誌佇列服務（NotificationLogQueueService）
    /// ---
    /// 管理推播日誌（NotificationLog）的 Memory Queue，負責併發安全入列、背景批次寫入資料庫，
    /// 減輕主流程壓力、提升效能，並支援批次重試與監控。
    /// 
    /// 流程說明：
    /// 1. Enqueue / EnqueueRange：將推播日誌加入 Memory Queue（併發安全），可單筆或多筆。
    /// 2. ExecuteAsync（BackgroundService）：
    ///     a. 持續監控 Semaphore，當有日誌入列時啟動消費流程。
    ///     b. 批次取出最多 BatchSize 筆日誌（或 queue 為空），寫入資料庫。
    ///     c. 寫入失敗時記錄錯誤並延遲重試，避免資料損失。
    /// 3. 日誌寫入流程與推播主流程解耦，確保高併發下穩定性與效能。
    /// </summary>
    public class NotificationLogQueueService 
    {
        private readonly DemoDbContext _db;
        private readonly ILogger<NotificationLogQueueService> _logger;
        private readonly ConcurrentQueue<NotificationLog> _queue = new();
        private readonly SemaphoreSlim _sem = new(0);

        // 可視需求調整 batch size
        private const int BatchSize = 1000;

        public NotificationLogQueueService(DemoDbContext db, ILogger<NotificationLogQueueService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 單筆入列（供主流程即時寫入 log）
        /// </summary>
        /// <param name="log">推播日誌物件</param>
        public void Enqueue(NotificationLog log)
        {
            _queue.Enqueue(log);
            _sem.Release();
            _logger.LogDebug("NotificationLog enqueued. DeviceInfoId={DeviceInfoId}, CreatedAt={CreatedAt}", log.DeviceInfoId, log.CreateAtUtc);
        }

        /// <summary>
        /// 批次入列（供主流程批次寫入 log）
        /// </summary>
        /// <param name="logs">推播日誌集合</param>
        public void EnqueueRange(IEnumerable<NotificationLog> logs)
        {
            int count = 0;
            foreach (var log in logs)
            {
                _queue.Enqueue(log);
                count++;
            }
            for (int i = 0; i < count; i++) _sem.Release();
            _logger.LogDebug("Batch NotificationLog enqueued. Count={Count}", count);
        }

        /// <summary>
        /// 取得目前 Memory Queue 待寫入日誌數量（監控用）
        /// </summary>
        public int GetQueueLength() => _queue.Count;

        public async Task ConsumeLogsAsync(CancellationToken stoppingToken)
        {
            var buffer = new List<NotificationLog>(BatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _sem.WaitAsync(stoppingToken);

                    while (buffer.Count < BatchSize && _queue.TryDequeue(out var log))
                    {
                        buffer.Add(log);
                    }

                    if (buffer.Count > 0)
                    {
                        _db.NotificationLogs.AddRange(buffer);
                        await _db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("NotificationLog batch saved. Count={Count}, RemainingInQueue={QueueLength}", buffer.Count, _queue.Count);
                        buffer.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch save to database failed. BufferCount={BufferCount}. Retrying in 3 seconds.", buffer.Count);
                    await Task.Delay(3000, stoppingToken);
                }
            }
        }
    }
}
