using Demo.Data;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services
{
    public class RetryService
    {
        private readonly DemoDbContext _db;
        private readonly NotificationService _notificationService;
        private readonly ILogger<RetryService> _logger;

        public RetryService(DemoDbContext db, NotificationService notificationService, ILogger<RetryService> logger)
        {
            _db = db;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task ProcessAllRetriesAsync()
        {
            try
            {
                // 讀取所有未成功且重試次數未達上限的紀錄
                var limits = await _db.NotificationLimitsConfigs.ToListAsync();
                foreach (var limit in limits)
                {
                    var failedLogs = _db.ExternalNotificationLogs
                        .Where(l => !l.NotificationStatus && l.RetryCount < limit.MaxAttempts)
                        .Take(limit.BatchSize)
                        .ToList();

                    foreach (var log in failedLogs)
                    {
                        try
                        {
                            var result = await _notificationService.RetrySendNotificationAsync(log);
                            if (result)
                            {
                                log.NotificationStatus = true;
                            }
                            log.RetryCount++;
                            log.UpdateAtUtc = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "重試單筆推播失敗 LogId={Id}", log.ExternalNotificationLogId);
                        }
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessAllRetriesAsync 全域例外: {Msg}", ex.Message);
            }
        }
    }
}
