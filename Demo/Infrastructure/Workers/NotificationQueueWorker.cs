using Demo.Infrastructure.Services;

namespace Demo.Infrastructure.Workers
{
    /// <summary>
    /// NotificationQueueWorker
    /// ---
    /// 持續從 NotificationQueueService 取出推播任務並處理。
    /// </summary>
    public class NotificationQueueWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationQueueWorker> _logger;

        public NotificationQueueWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationQueueWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification queue worker execution started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var queueService = scope.ServiceProvider.GetRequiredService<NotificationQueueService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

                        var queueItem = await queueService.DequeueAsync();
                        if (queueItem != null)
                        {
                            var delay = DateTime.UtcNow - queueItem.EnqueueTime;

                            // 1. 處理任務前記錄細節
                            _logger.LogInformation(
                                "Processing notification: Source={Source}, DeviceIds={DeviceInfoIds}, Title={Title}, EnqueueTime={EnqueueTime}, DequeueTime={DequeueTime}, DelaySeconds={DelaySeconds}",
                                queueItem.Request.Source,
                                queueItem.Request.DeviceInfoIds,
                                queueItem.Request.Title,
                                queueItem.EnqueueTime,
                                DateTime.UtcNow,
                                delay.TotalSeconds
                                );

                            // 2, 實際推播
                            var result = await notificationService.NotifyByTargetAsync(queueItem.Request);

                            if (!result.IsSuccess)
                            {
                                _logger.LogWarning("Notification send failed. Source={Source}, Message={Message}", queueItem.Request.Source, result.Message);
                            }

                            // 3. 結果回報
                            _logger.LogInformation(
                                "Notification processed. Source={Source}, Success={Success}, Message={Message}, DeviceIds={DeviceInfoIds}, Title={Title}",
                                queueItem.Request.Source,
                                result.IsSuccess,
                                result.Message,
                                queueItem.Request.DeviceInfoIds,
                                queueItem.Request.Title
                             );
                        }
                        else
                        {
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during task consumption. Retrying in 3 seconds.");
                    await Task.Delay(3000, stoppingToken);
                }
            }
            _logger.LogInformation("Exiting");
        }
    }
}
