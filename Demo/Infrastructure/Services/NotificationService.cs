using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services.Notification;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services
{
    public class NotificationService
    {
        private readonly DemoDbContext _db;
        private readonly ILogger<NotificationService> _logger;
        private readonly AppNotificationStrategy _appStrategy;
        private readonly WebNotificationStrategy _webStrategy;
        private readonly EmailNotificationStrategy _emailStrategy;
        private readonly LineNotificationStrategy _lineStrategy;
        private readonly NotificationQueueService _queueService;

        public NotificationService(DemoDbContext db, ILogger<NotificationService> logger, AppNotificationStrategy appStrategy, WebNotificationStrategy webStrategy, EmailNotificationStrategy emailStrategy, LineNotificationStrategy lineStrategy, NotificationQueueService queueService)
        {
            _db = db;
            _logger = logger;
            _appStrategy = appStrategy;
            _webStrategy = webStrategy;
            _emailStrategy = emailStrategy;
            _lineStrategy = lineStrategy;
            _queueService = queueService;
        }

        /// <summary>
        /// 推播主要入口，可選同步發送或進 queue
        /// </summary>
        public async Task<NotificationResultResponse> NotifyAsync(
            NotificationRequestDto request,
            NotificationSourceType source,
            bool useQueue = false)
        {
            if (useQueue)
            {
                // 進 queue 非同步發送
                await _queueService.EnqueueAsync(request, source);
                _logger.LogInformation("推播加入佇列，來源: {Source}, Request: {@Request}", source, request);
                return new NotificationResultResponse { IsSuccess = true, Message = "已加入推播佇列" };
            }
            else
            {
                // 直接同步推播
                return await NotifyByTargetAsync(request, source);
            }
        }

        /// <summary>
        /// 依據條件查詢裝置，組合推播資料並發送
        /// </summary>
        public async Task<NotificationResultResponse> NotifyByTargetAsync(NotificationRequestDto request, NotificationSourceType source)
        {
            var result = new NotificationResultResponse();

            // 1. 查詢目標裝置
            var devices = await GetTargetDevicesAsync(request);
            if (!devices.Any())
            {
                result.IsSuccess = false;
                result.Message = "查無推播目標裝置";
                return result;
            }

            NotificationMsgTemplate? template = null;
            Dictionary<string, string> customData = new();
            string? title = null;
            string? body = null;

            // 2. 決定推播內容來源（templateId 或 code）
            if (request.NotificationMsgTemplateId != null)
            {
                // 2a. 以 templateId 查找模板與自訂資料
                template = await _db.NotificationMsgTemplates
                    .FirstOrDefaultAsync(t => t.NotificationMsgTemplateId == request.NotificationMsgTemplateId.Value);
                if (template == null)
                {
                    result.IsSuccess = false;
                    result.Message = "查無訊息模板";
                    return result;
                }
                var customDataList = await _db.NotificationMsgTemplateDatas
                    .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                    .ToListAsync();
                customData = customDataList.ToDictionary(d => d.Key, d => d.Value);

                // 若有自訂 title/body 優先，否則用 template.gw
                title = request.Title ?? template.Gw;
                body = request.Body ?? template.Gw;
            }
            else
            {
                // 2b. 以 code 查代碼資訊，並找對應模板與自訂資料
                var lang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : devices.FirstOrDefault()?.Lang ?? "zh-TW";
                var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(x => x.Code == request.Code && x.Lang == lang);
                if (codeInfo == null)
                {
                    result.IsSuccess = false;
                    result.Message = "查無對應通知代碼";
                    return result;
                }

                var gw = devices.FirstOrDefault()?.Gw;
                template = await _db.NotificationMsgTemplates
                    .Where(x => x.CodeInfoId == codeInfo.CodeInfoId && x.Gw == gw)
                    .FirstOrDefaultAsync()
                    ?? await _db.NotificationMsgTemplates
                        .Where(x => x.CodeInfoId == codeInfo.CodeInfoId)
                        .FirstOrDefaultAsync();

                if (template == null)
                {
                    result.IsSuccess = false;
                    result.Message = "查無訊息模板";
                    return result;
                }

                var customDataList = await _db.NotificationMsgTemplateDatas
                    .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                    .ToListAsync();
                customData = customDataList.ToDictionary(d => d.Key, d => d.Value);

                title = codeInfo.Title ?? "";
                body = codeInfo.Body ?? "";
            }

            // 3. 依策略分流批次推播
            return await NotifyByTargetAsyncInternal(devices, title, body, template, customData, source);
        }

        /// <summary>
        /// 根據失敗 Log 直接重發（只用 log 內容）
        /// </summary>
        public async Task<bool> RetrySendNotificationAsync(object log, bool isBackend = false)
        {
            // 1. 解析 Log 欄位
            Guid deviceInfoId;
            string title, body;
            int retryCount;
            bool notificationStatus;
            DateTime lastUpdateAt;

            if (isBackend && log is BackendNotificationLog backendLog)
            {
                deviceInfoId = backendLog.DeviceInfoId;
                title = backendLog.Title;
                body = backendLog.Body;
                retryCount = backendLog.RetryCount;
                notificationStatus = backendLog.NotificationStatus;
                lastUpdateAt = backendLog.UpdateAtUtc ?? backendLog.CreateAtUtc;
            }
            else if (!isBackend && log is ExternalNotificationLog externalLog)
            {
                deviceInfoId = externalLog.DeviceInfoId;
                title = externalLog.Title;
                body = externalLog.Body;
                retryCount = externalLog.RetryCount;
                notificationStatus = externalLog.NotificationStatus;
                lastUpdateAt = externalLog.UpdateAtUtc ?? externalLog.CreateAtUtc;
            }
            else
            {
                _logger.LogWarning("RetrySendNotificationAsync: Log型別不正確");
                return false;
            }

            // 2. 已發送成功則不重發
            if (notificationStatus) return true;

            // 3. 查裝置
            var device = await _db.DeviceInfos.FirstOrDefaultAsync(x => x.DeviceInfoId == deviceInfoId && x.Status);
            if (device == null) return false;

            var devices = new List<DeviceInfo> { device };
            var customData = new Dictionary<string, string>();
            NotificationMsgTemplate? template = null;

            // 4. 呼叫推播策略（只用 title/body，不查 template），Retry 時，不寫新 log（writeLog: false）
            var result = await NotifyByTargetAsyncInternal(devices, title, body, template, customData, NotificationSourceType.Backend, writeLog: false);

            // 5. 根據結果更新原本 log
            bool isSuccess = result.IsSuccess;
            string msg = isSuccess ? "重發成功" : (string.IsNullOrWhiteSpace(result.Message) ? "重發失敗" : $"重發失敗: {result.Message}");

            if (isBackend && log is BackendNotificationLog backend)
            {
                backend.NotificationStatus = isSuccess;
                backend.RetryCount++;
                backend.UpdateAtUtc = DateTime.UtcNow;
                backend.ResultMsg = msg;
            }
            else if (!isBackend && log is ExternalNotificationLog external)
            {
                external.NotificationStatus = isSuccess;
                external.RetryCount++;
                external.UpdateAtUtc = DateTime.UtcNow;
                external.ResultMsg = msg;
            }

            return isSuccess;
        }

        #region private methods
        /// <summary>
        /// 依裝置推播啟用狀態分流，分批呼叫各推播策略
        /// </summary>
        private async Task<NotificationResultResponse> NotifyByTargetAsyncInternal(
            List<DeviceInfo> devices,
            string title,
            string body,
            NotificationMsgTemplate template,
            Dictionary<string, string> customData,
            NotificationSourceType source,
            bool writeLog = true)
        {
            var result = new NotificationResultResponse();

            // 通道失敗旗標 (thread-safe)
            int appFailed = 0, webFailed = 0, emailFailed = 0, lineFailed = 0;

            // 1. 查詢裝置對應通道啟用狀態
            var deviceGuidList = devices.Select(d => d.DeviceInfoId).ToList();
            var notificationTypes = await _db.NotificationTypes
                .Where(x => deviceGuidList.Contains(x.DeviceInfoId))
                .ToDictionaryAsync(x => x.DeviceInfoId);

            var appDevices = new List<DeviceInfo>();
            var webDevices = new List<DeviceInfo>();
            var emailDevices = new List<DeviceInfo>();
            var lineDevices = new List<DeviceInfo>();

            foreach (var device in devices)
            {
                if (!notificationTypes.TryGetValue(device.DeviceInfoId, out var nType)) continue;
                if (nType.IsAppActive) appDevices.Add(device);
                if (nType.IsWebActive) webDevices.Add(device);
                if (nType.IsEmailActive) emailDevices.Add(device);
                if (nType.IsLineActive) lineDevices.Add(device);
            }

            // 2. 取得各通道推播限流/批次設定
            var limits = await _db.NotificationLimitsConfigs.ToListAsync();

            // APP
            var appLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.App);
            int appBatchSize = appLimit?.BatchSize ?? 500;
            int appMaxConcurrent = appLimit?.MaxConcurrentTasks ?? 5;
            var appSemaphore = new SemaphoreSlim(appMaxConcurrent);

            // WEB
            var webLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Web);
            int webBatchSize = webLimit?.BatchSize ?? 500;
            int webMaxConcurrent = webLimit?.MaxConcurrentTasks ?? 5;
            var webSemaphore = new SemaphoreSlim(webMaxConcurrent);

            // EMAIL
            var emailLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Email);
            int emailBatchSize = emailLimit?.BatchSize ?? 1000;
            int emailMaxConcurrent = emailLimit?.MaxConcurrentTasks ?? 5;
            var emailSemaphore = new SemaphoreSlim(emailMaxConcurrent);

            // LINE
            var lineLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Line);
            int lineBatchSize = lineLimit?.BatchSize ?? 500;
            int lineMaxConcurrent = lineLimit?.MaxConcurrentTasks ?? 5;
            var lineSemaphore = new SemaphoreSlim(lineMaxConcurrent);

            var tasks = new List<Task>();

            // ========== APP 分批 ==========
            foreach (var group in appDevices.GroupBy(d => d.ProductInfoId))
            {
                var deviceBatch = group.ToList();
                foreach (var batch in Batch(deviceBatch, appBatchSize))
                {
                    await appSemaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _appStrategy.SendAsync(batch, title, body, customData, template);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "App推播已發送");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref appFailed, 1);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"App推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "App推播失敗");
                        }
                        finally
                        {
                            appSemaphore.Release();
                        }
                    }));
                }
            }

            // ========== WEB 分批 ==========
            foreach (var group in webDevices.GroupBy(d => d.ProductInfoId))
            {
                var deviceBatch = group.ToList();
                foreach (var batch in Batch(deviceBatch, webBatchSize))
                {
                    await webSemaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _webStrategy.SendAsync(batch, title, body, customData, template);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Web推播已發送");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref webFailed, 1);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Web推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "Web推播失敗");
                        }
                        finally
                        {
                            webSemaphore.Release();
                        }
                    }));
                }
            }

            // ========== EMAIL 分批 ==========
            foreach (var group in emailDevices.GroupBy(d => d.ProductInfoId))
            {
                var deviceBatch = group.ToList();
                foreach (var batch in Batch(deviceBatch, emailBatchSize))
                {
                    await emailSemaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _emailStrategy.SendAsync(batch, title, body, customData, template);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Email已發送");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref emailFailed, 1);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Email推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "Email推播失敗");
                        }
                        finally
                        {
                            emailSemaphore.Release();
                        }
                    }));
                }
            }

            // ========== LINE 分批 ==========
            foreach (var group in lineDevices.GroupBy(d => d.ProductInfoId))
            {
                var deviceBatch = group.ToList();
                foreach (var batch in Batch(deviceBatch, lineBatchSize))
                {
                    await lineSemaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _lineStrategy.SendAsync(batch, title, body, customData, template);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Line已發送");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref lineFailed, 1);
                            foreach (var device in batch)
                            {
                                if (writeLog)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Line推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "Line推播失敗");
                        }
                        finally
                        {
                            lineSemaphore.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // log 全部寫入
            if (writeLog)
            {
                await _db.SaveChangesAsync();
            }

            // 決定結果
            result.IsSuccess = (appFailed == 0 && webFailed == 0 && emailFailed == 0 && lineFailed == 0);

            var failList = new List<string>();
            if (appFailed != 0) failList.Add("APP");
            if (webFailed != 0) failList.Add("WEB");
            if (emailFailed != 0) failList.Add("EMAIL");
            if (lineFailed != 0) failList.Add("LINE");

            if (failList.Count == 0)
                result.Message = $"推播已送出，共 {devices.Count} 台裝置";
            else
                result.Message = $"推播部份通道({string.Join(",", failList)})有失敗，共 {devices.Count} 台裝置";

            return result;
        }

        /// <summary>
        /// 查詢推播目標裝置（依 DeviceIds、NotificationGroup、全部）
        /// </summary>
        private async Task<List<DeviceInfo>> GetTargetDevicesAsync(NotificationRequestDto request)
        {
            if (request.DeviceIds != null && request.DeviceIds.Any())
            {
                return await _db.DeviceInfos
                    .Where(x => request.DeviceIds.Contains(x.DeviceId) && x.Status)
                    .ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(request.NotificationGroup))
            {
                return await _db.DeviceInfos
                    .Where(x => x.NotificationGroup == request.NotificationGroup && x.Status)
                    .ToListAsync();
            }
            else
            {
                return await _db.DeviceInfos
                    .Where(x => x.Status)
                    .ToListAsync();
            }
        }

        /// <summary>
        /// List 分批工具
        /// </summary>
        private IEnumerable<List<T>> Batch<T>(List<T> source, int batchSize)
        {
            for (int i = 0; i < source.Count; i += batchSize)
                yield return source.GetRange(i, Math.Min(batchSize, source.Count - i));
        }

        private void WriteNotificationLog(NotificationSourceType source, DeviceInfo device, string title, string body, bool status, string msg)
        {
            var now = DateTime.UtcNow;
            switch (source)
            {
                case NotificationSourceType.Backend:
                    _db.BackendNotificationLogs.Add(new BackendNotificationLog
                    {
                        BackendNotificationLogId = Guid.NewGuid(),
                        DeviceInfoId = device.DeviceInfoId,
                        Gw = device.Gw,
                        Title = title,
                        Body = body,
                        NotificationStatus = status,
                        ResultMsg = msg,
                        RetryCount = 0,
                        CreateAtUtc = now
                    });
                    break;
                case NotificationSourceType.External:
                    _db.ExternalNotificationLogs.Add(new ExternalNotificationLog
                    {
                        ExternalNotificationLogId = Guid.NewGuid(),
                        DeviceInfoId = device.DeviceInfoId,
                        Gw = device.Gw,
                        Title = title,
                        Body = body,
                        NotificationStatus = status,
                        ResultMsg = msg,
                        RetryCount = 0,
                        CreateAtUtc = now
                    });
                    break;
                    // ... 若有其它來源類型請加上
            }
        }

        #endregion private methods
    }
}
