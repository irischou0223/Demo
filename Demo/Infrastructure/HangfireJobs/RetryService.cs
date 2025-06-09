using Castle.Components.DictionaryAdapter.Xml;
using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Hangfire
{
    /// <summary>
    /// 失敗通知重試服務
    /// </summary>
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

        /// <summary>
        /// 依四通道 (APP/WEB/EMAIL/LINE) 與 Log 類型 (External/Backend) 做批次重試
        /// </summary>
        public async Task ProcessAllRetriesAsync()
        {
            try
            {
                // 1. 取得所有通道重試限制設定
                var limits = await _db.NotificationLimitsConfigs.ToListAsync();

                // 2. 撈出 ExternalNotificationLog 尚未成功的資料
                var externalLogs = await _db.ExternalNotificationLogs
                    .Where(l => !l.NotificationStatus)
                    .OrderBy(l => l.UpdateAtUtc ?? l.CreateAtUtc)
                    .ToListAsync();

                // 3. 依通道策略重發 External logs
                await ProcessLogsByChannel(externalLogs, limits, isBackend: false);

                // 4. 撈出 BackendNotificationLog 尚未成功的資料
                var backendLogs = await _db.BackendNotificationLogs
                    .Where(l => !l.NotificationStatus)
                    .OrderBy(l => l.UpdateAtUtc ?? l.CreateAtUtc)
                    .ToListAsync();

                // 5. 依通道策略重發 Backend log
                await ProcessLogsByChannel(backendLogs, limits, isBackend: true);

                // 6. 儲存所有結果
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessAllRetriesAsync 全域例外: {Msg}", ex.Message);
            }
        }

        /// <summary>
        /// 根據通道分組，分別依限流設定重試
        /// </summary>
        private async Task ProcessLogsByChannel<T>(List<T> logs, List<NotificationLimitsConfig> limits, bool isBackend)
        where T : class
        {
            // 1. 撈出這批 log 所有的 deviceId，查出通道啟用狀態
            var deviceIdList = logs switch
            {
                List<ExternalNotificationLog> extLogs => extLogs.Select(x => x.DeviceInfoId).ToList(),
                List<BackendNotificationLog> backLogs => backLogs.Select(x => x.DeviceInfoId).ToList(),
                _ => new List<Guid>()
            };

            var deviceChannels = await _db.NotificationTypes
                .Where(x => deviceIdList.Contains(x.DeviceInfoId))
                .ToDictionaryAsync(x => x.DeviceInfoId);

            var now = DateTime.UtcNow;

            // 2. 針對每一種通道做分組與判斷
            foreach (var channel in new[] {
                NotificationChannelType.App,
                NotificationChannelType.Web,
                NotificationChannelType.Email,
                NotificationChannelType.Line })
            {
                // 2.1 取得該通道的策略設定
                var limit = limits.FirstOrDefault(x => x.NotificationType == channel);
                if (limit == null) continue;

                int maxAttempts = limit.MaxAttempts;
                int initialDelay = limit.InitialRetryDelaySeconds;
                int maxDelay = limit.MaxRetryDelaySeconds;
                double backoff = (double)limit.BackoffMultiplier;
                int maxDuration = limit.MaxRetryDurationSeconds;

                // 2.2 選出這個通道且符合 retry 條件的 log
                var logsForChannel = logs.Where(log =>
                {
                    Guid deviceId = log switch
                    {
                        ExternalNotificationLog ext => ext.DeviceInfoId,
                        BackendNotificationLog back => back.DeviceInfoId,
                        _ => Guid.Empty
                    };

                    if (deviceId == Guid.Empty || !deviceChannels.TryGetValue(deviceId, out var nType)) return false;

                    // 判斷裝置該通道是否啟用
                    bool channelActive = channel switch
                    {
                        NotificationChannelType.App => nType.IsAppActive,
                        NotificationChannelType.Web => nType.IsWebActive,
                        NotificationChannelType.Email => nType.IsEmailActive,
                        NotificationChannelType.Line => nType.IsLineActive,
                        _ => false
                    };

                    int retryCount = log switch
                    {
                        ExternalNotificationLog ext2 => ext2.RetryCount,
                        BackendNotificationLog back2 => back2.RetryCount,
                        _ => 0
                    };

                    DateTime firstSendAt = log switch
                    {
                        ExternalNotificationLog ext2 => ext2.CreateAtUtc,
                        BackendNotificationLog back2 => back2.CreateAtUtc,
                        _ => DateTime.MinValue
                    };

                    DateTime lastRetryAt = log switch
                    {
                        ExternalNotificationLog ext2 => ext2.UpdateAtUtc ?? ext2.CreateAtUtc,
                        BackendNotificationLog back2 => back2.UpdateAtUtc ?? back2.CreateAtUtc,
                        _ => DateTime.MinValue
                    };

                    // 2.2.1 達最大重試次數則不再 retry
                    if (retryCount >= maxAttempts) return false;

                    // 2.2.2 超過最大允許重試時長則不再 retry
                    if ((now - firstSendAt).TotalSeconds > maxDuration) return false;

                    // 2.2.3 計算本次 retry 應等待的秒數（指數退避）
                    double delay = initialDelay * Math.Pow(backoff, Math.Max(0, retryCount - 1));
                    delay = Math.Min(delay, maxDelay);

                    // 2.2.4 距離上次 retry 是否已滿足 delay
                    bool intervalEnough = (now - lastRetryAt).TotalSeconds >= delay;

                    return channelActive && intervalEnough;
                }).ToList();

                foreach (var log in logsForChannel)
                {
                    try
                    {
                        var result = await _notificationService.RetrySendNotificationAsync(log, isBackend);
                        IncrementLogStatus(log, result);
                    }
                    catch (Exception ex)
                    {
                        SetLogResultMsg(log, $"重發例外: {ex.Message}");
                        _logger.LogError(ex, "重試 {Channel} 推播失敗 LogId={Id}", channel, GetLogId(log));
                    }
                }
            }
        }

        /// <summary>
        /// 更新 log 狀態（重試次數＋1、狀態、訊息、時間）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="log"></param>
        /// <param name="success"></param>
        private void IncrementLogStatus<T>(T log, bool success)
        {
            var now = DateTime.UtcNow;
            if (log is ExternalNotificationLog ext)
            {
                ext.NotificationStatus = success;
                ext.RetryCount++;
                ext.UpdateAtUtc = now;
                ext.ResultMsg = success ? "重發成功" : "重發失敗";
            }
            else if (log is BackendNotificationLog back)
            {
                back.NotificationStatus = success;
                back.RetryCount++;
                back.UpdateAtUtc = now;
                back.ResultMsg = success ? "重發成功" : "重發失敗";
            }
        }

        /// <summary>
        /// 設定 log 的訊息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="log"></param>
        /// <param name="msg"></param>
        private void SetLogResultMsg<T>(T log, string msg)
        {
            if (log is ExternalNotificationLog ext)
                ext.ResultMsg = msg;
            else if (log is BackendNotificationLog back)
                back.ResultMsg = msg;
        }

        /// <summary>
        /// 取得 log 主鍵
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="log"></param>
        /// <returns></returns>
        private Guid GetLogId<T>(T log)
        {
            if (log is ExternalNotificationLog ext)
                return ext.ExternalNotificationLogId;
            if (log is BackendNotificationLog back)
                return back.BackendNotificationLogId;
            return Guid.Empty;
        }
    }
}
