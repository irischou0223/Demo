using Demo.Infrastructure.Services;

namespace Demo.Infrastructure.Workers
{
    public class NotificationLogQueueWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationLogQueueWorker> _logger;

        public NotificationLogQueueWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationLogQueueWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationLogQueueWorker started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var logQueueService = scope.ServiceProvider.GetRequiredService<NotificationLogQueueService>();
                    await logQueueService.ConsumeLogsAsync(stoppingToken);
                }
                await Task.Delay(1000, stoppingToken); // 控制輪詢頻率
            }
            _logger.LogInformation("NotificationLogQueueWorker stopped.");
        }
    }
}
