using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services.Notification;
using Demo.Models.DTOs;
using Medo;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 推播服務總管，負責協調查詢目標、分流至各推播策略、限流、失敗重試與日誌記錄。
    /// 流程說明：
    /// 1. NotifyAsync：推播請求入口，決定直發或進 queue。
    /// 2. NotifyByTargetAsync：依 target 查裝置、組出推播內容、流向各通道策略。
    /// 3. NotifyByTargetAsyncInternal：依裝置推播狀態，限流分批，調用各策略（App/Web/Email/Line）。
    /// 4. 失敗可用 RetrySendNotificationAsync 依 log 重發。
    /// 5. 內部會自動寫入發送 Log（依通道/來源不同）。
    /// </summary>
    public class NotificationService
    {
        private readonly DemoDbContext _db;
        private readonly ILogger<NotificationService> _logger;
        private readonly AppNotificationStrategy _appStrategy;
        private readonly WebNotificationStrategy _webStrategy;
        private readonly EmailNotificationStrategy _emailStrategy;
        private readonly LineNotificationStrategy _lineStrategy;
        private readonly NotificationQueueService _queueService;
        private readonly NotificationLogQueueService _logQueueService;

        public NotificationService(DemoDbContext db, ILogger<NotificationService> logger, AppNotificationStrategy appStrategy, WebNotificationStrategy webStrategy, EmailNotificationStrategy emailStrategy, LineNotificationStrategy lineStrategy, NotificationQueueService queueService, NotificationLogQueueService logQueueService)
        {
            _db = db;
            _logger = logger;
            _appStrategy = appStrategy;
            _webStrategy = webStrategy;
            _emailStrategy = emailStrategy;
            _lineStrategy = lineStrategy;
            _queueService = queueService;
            _logQueueService = logQueueService;
        }

        /// <summary>
        /// 推播主要入口，可選同步發送或進 queue
        /// </summary>
        /// <param name="request">推播請求物件</param>
        /// <param name="source">推播來源（Backend/External）</param>
        /// <param name="useQueue">是否進 queue 非同步處理</param>
        /// <returns>推播結果</returns>
        public async Task<NotificationResponseDto> NotifyAsync(NotificationRequestDto request, bool useQueue = false)
        {
            _logger.LogInformation("Notification request received. Source={Source}, UseQueue={UseQueue}, DeviceCount={DeviceCount}, Lang={Lang}, NotificationMsgTemplateId={TemplateId}, NotificationGroup={NotificationGroup}",
                request?.Source, useQueue, request?.DeviceInfoIds?.Count ?? 0, request?.Lang, request?.NotificationMsgTemplateId, request?.NotificationGroup);

            NotificationResponseDto result;
            if (useQueue)
            {
                // 進 queue 非同步發送
                _logger.LogInformation("Notification enqueuing to queue. Source={Source}", request.Source);
                await _queueService.EnqueueAsync(request);
                _logger.LogInformation("Notification successfully enqueued. Source={Source}", request.Source);
                result = new NotificationResponseDto { IsSuccess = true, Message = "Notification enqueued for async processing." };
            }
            else
            {
                // 直接同步推播
                result = await NotifyByTargetAsync(request);
            }
            _logger.LogInformation("Notification processing completed. Source={Source}, UseQueue={UseQueue}, IsSuccess={IsSuccess}, Message={Message}", request.Source, useQueue, result.IsSuccess, result.Message);
            return result;
        }

        /// <summary>
        /// 依據條件查詢裝置，組合推播資料並發送
        /// 1. 查詢目標裝置
        /// 2. 決定推播內容來源（templateId 或 code）
        /// 3. 分流調用推播
        /// </summary>
        public async Task<NotificationResponseDto> NotifyByTargetAsync(NotificationRequestDto request)
        {
            _logger.LogInformation("NotifyByTargetAsync started. Source={Source}, DeviceInfoIds={DeviceInfoIds}, NotificationMsgTemplateId={TemplateId}, Code={Code}",
                request.Source, request.DeviceInfoIds, request.NotificationMsgTemplateId, request.Code);

            var result = new NotificationResponseDto();

            // 1. 查詢目標裝置
            var devices = await GetTargetDevicesAsync(request);
            _logger.LogInformation("Target device query completed. DeviceCount={DeviceCount}", devices.Count);

            if (!devices.Any())
            {
                result.IsSuccess = false;
                result.Message = "No target devices found for notification.";
                _logger.LogWarning("No target devices found for notification. Request parameters: {@Request}", request);
                return result;
            }

            NotificationMsgTemplate? template = null;
            Dictionary<string, string> customData = new();
            string? title = null;
            string? body = null;

            // 2. 決定推播內容來源（templateId 或 code）
            if (request.NotificationMsgTemplateId != null)
            {
                // [補充說明] 以 templateId 查找訊息模板，常用於自訂訊息模板情境
                _logger.LogInformation("Using NotificationMsgTemplateId={TemplateId} for notification content.", request.NotificationMsgTemplateId);
                template = await _db.NotificationMsgTemplates
                    .FirstOrDefaultAsync(t => t.NotificationMsgTemplateId == request.NotificationMsgTemplateId.Value);
                if (template == null)
                {
                    result.IsSuccess = false;
                    result.Message = "Notification message template not found.";
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
                // [補充說明] 以 code 查找標準訊息模板，常用於依規格定義的訊息通知
                _logger.LogInformation("Using notification code: {Code}", request.Code);
                var lang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : devices.FirstOrDefault()?.Lang ?? "zh-TW";
                var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(x => x.Code == request.Code && x.Lang == lang);
                if (codeInfo == null)
                {
                    result.IsSuccess = false;
                    result.Message = "查無對應通知代碼";
                    _logger.LogWarning("No matching notification code. Code={Code}, Lang={Lang}", request.Code, lang);
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
                    _logger.LogWarning("Notification message template not found. CodeInfoId={CodeInfoId}", codeInfo.CodeInfoId);
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
            result = await NotifyByTargetAsyncInternal(devices, title, body, template, customData, request.Source);

            _logger.LogInformation("NotifyByTargetAsync completed. Source={Source}, IsSuccess={IsSuccess}, Message={Message}", request.Source, result.IsSuccess, result.Message);

            return result;
        }

        /// <summary>
        /// 根據失敗 Log 直接重發（只用 log 內容）
        /// </summary>
        public async Task<bool> RetrySendNotificationAsync(NotificationLog log)
        {
            _logger.LogInformation("RetrySendNotificationAsync started. NotificationLogId={LogId}, Source={Source}", log.NotificationLogId, log.NotificationSource);

            // 1. 已發送成功則不重發
            if (log.NotificationStatus)
            {
                _logger.LogInformation("RetrySendNotificationAsync skipped - already sent. NotificationLogId={LogId}", log.NotificationLogId);
                return true;
            }

            // 2. 查裝置
            var device = await _db.DeviceInfos.FirstOrDefaultAsync(x => x.DeviceInfoId == log.DeviceInfoId && x.Status);
            if (device == null)
            {
                _logger.LogWarning("RetrySendNotificationAsync failed - device not found. DeviceInfoId={DeviceInfoId}", log.DeviceInfoId);
                return false;
            }

            var devices = new List<DeviceInfo> { device };
            var customData = new Dictionary<string, string>();
            NotificationMsgTemplate? template = null;

            // 3.只用 log 上的 title/body，不查 template。Retry 時，不再寫新 log
            var result = await NotifyByTargetAsyncInternal(devices, log.Title, log.Body, template, customData, log.NotificationSource, writeLog: false);

            // 4.更新原本 log
            log.NotificationStatus = result.IsSuccess;
            log.RetryCount++;
            log.UpdateAtUtc = DateTime.UtcNow;
            log.ResultMsg = result.IsSuccess ? "Retry succeeded" : $"Retry failed: {result.Message}";

            _logger.LogInformation("RetrySendNotificationAsync completed. NotificationLogId={LogId}, Success={IsSuccess}", log.NotificationLogId, result.IsSuccess);
            await _db.SaveChangesAsync();
            return result.IsSuccess;
        }

        #region private methods

        /// <summary>
        /// 分流通道，依各裝置推播啟用狀態分批推播，支援多種限流與批次控制（App/Web/Email/Line）
        /// 1. 取得各裝置的推播啟用設定
        /// 2. 依通道分流、分批、限流
        /// 3. 並行發送，記錄回應與失敗，依需寫入通知 Log
        /// </summary>
        private async Task<NotificationResponseDto> NotifyByTargetAsyncInternal(
            List<DeviceInfo> devices,
            string title,
            string body,
            NotificationMsgTemplate template,
            Dictionary<string, string> customData,
            NotificationSourceType source,
            Guid? notificationScheduledJobId = null,
            bool writeLog = true)
        {
            _logger.LogInformation("Notification batch send started. DeviceCount={DeviceCount}, Title={Title}, Source={Source}", devices.Count, title, source);

            var result = new NotificationResponseDto();

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

            // 根據裝置數量自動調整 batch size (例如 1000 台以下 500，1000~5000 台 1000，5000 台以上 2000)
            int GetDynamicBatchSize(int count)
            {
                if (count > 5000) return 2000;
                if (count > 1000) return 1000;
                return 500;
            }

            // APP
            var appLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.App);
            int appBatchSize = appLimit?.BatchSize ?? GetDynamicBatchSize(appDevices.Count);
            int appMaxConcurrent = appLimit?.MaxConcurrentTasks ?? 5;
            var appSemaphore = new SemaphoreSlim(appMaxConcurrent);

            // WEB
            var webLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Web);
            int webBatchSize = webLimit?.BatchSize ?? GetDynamicBatchSize(webDevices.Count);
            int webMaxConcurrent = webLimit?.MaxConcurrentTasks ?? 5;
            var webSemaphore = new SemaphoreSlim(webMaxConcurrent);

            // EMAIL
            var emailLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Email);
            int emailBatchSize = emailLimit?.BatchSize ?? GetDynamicBatchSize(emailDevices.Count);
            int emailMaxConcurrent = emailLimit?.MaxConcurrentTasks ?? 5;
            var emailSemaphore = new SemaphoreSlim(emailMaxConcurrent);

            // LINE
            var lineLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Line);
            int lineBatchSize = lineLimit?.BatchSize ?? GetDynamicBatchSize(lineDevices.Count);
            int lineMaxConcurrent = lineLimit?.MaxConcurrentTasks ?? 5;
            var lineSemaphore = new SemaphoreSlim(lineMaxConcurrent);

            var tasks = new List<Task>();
            var logs = new List<NotificationLog>(devices.Count);

            // ========== APP 分批 ==========
            foreach (var group in appDevices.GroupBy(d => d.ProductInfoId))
            {
                var deviceBatch = group.ToList();
                foreach (var batch in Batch(deviceBatch, appBatchSize))
                {
                    await appSemaphore.WaitAsync();
                    var batchCopy = batch; // 避免 closure 問題
                    tasks.Add(Task.Run(async () =>
                    {
                        _logger.LogInformation("Sending APP notification batch. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        try
                        {
                            await _appStrategy.SendAsync(batchCopy, title, body, customData, template);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, true, "App notification sent", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogInformation("APP notification batch sent. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref appFailed, 1);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, false, $"App notification failed: {ex.Message}", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogError(ex, "APP notification batch failed. ProductInfoId={ProductInfoId}", group.Key);
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
                    var batchCopy = batch;
                    tasks.Add(Task.Run(async () =>
                    {
                        _logger.LogInformation("Sending WEB notification batch. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        try
                        {
                            await _webStrategy.SendAsync(batchCopy, title, body, customData, template);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, true, "Web notification sent", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogInformation("WEB notification batch sent. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref webFailed, 1);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, false, $"Web notification failed: {ex.Message}", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogError(ex, "WEB notification batch failed. ProductInfoId={ProductInfoId}", group.Key);
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
                    var batchCopy = batch;
                    tasks.Add(Task.Run(async () =>
                    {
                        _logger.LogInformation("Sending EMAIL notification batch. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        try
                        {
                            await _emailStrategy.SendAsync(batchCopy, title, body, customData, template);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, true, "Email notification sent", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogInformation("EMAIL notification batch sent. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref emailFailed, 1);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, false, $"Email notification failed: {ex.Message}", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogError(ex, "EMAIL notification batch failed. ProductInfoId={ProductInfoId}", group.Key);
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
                    var batchCopy = batch;
                    tasks.Add(Task.Run(async () =>
                    {
                        _logger.LogInformation("Sending LINE notification batch. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        try
                        {
                            await _lineStrategy.SendAsync(batchCopy, title, body, customData, template);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, true, "Line notification sent", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogInformation("LINE notification batch sent. DeviceCount={BatchCount}, ProductInfoId={ProductInfoId}", batchCopy.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref lineFailed, 1);
                            if (writeLog)
                            {
                                lock (logs)
                                {
                                    logs.AddRange(batchCopy.Select(device => WriteNotificationLog(source, device, title, body, false, $"Line notification failed: {ex.Message}", notificationScheduledJobId)));
                                }
                            }
                            _logger.LogError(ex, "LINE notification batch failed. ProductInfoId={ProductInfoId}", group.Key);
                        }
                        finally
                        {
                            lineSemaphore.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // 批次分批寫入 log
            if (writeLog && logs.Any())
            {
                _logQueueService.EnqueueRange(logs);
            }

            // 決定結果
            result.IsSuccess = (appFailed == 0 && webFailed == 0 && emailFailed == 0 && lineFailed == 0);

            var failList = new List<string>();
            if (appFailed != 0) failList.Add("APP");
            if (webFailed != 0) failList.Add("WEB");
            if (emailFailed != 0) failList.Add("EMAIL");
            if (lineFailed != 0) failList.Add("LINE");

            if (failList.Count == 0)
            {
                result.Message = $"Notification sent to {devices.Count} devices.";
            }
            else
            {
                result.Message = $"Partial notification failure on channel(s): {string.Join(",", failList)}. Total devices: {devices.Count}.";
            }

            _logger.LogInformation("Notification batch send completed. DeviceCount={DeviceCount}, IsSuccess={IsSuccess}, Message={Message}", devices.Count, result.IsSuccess, result.Message);

            return result;
        }

        /// <summary>
        /// 建立單一 NotificationLog 物件
        /// </summary>
        private NotificationLog WriteNotificationLog(
            NotificationSourceType source,
            DeviceInfo device,
            string title,
            string body,
            bool status,
            string msg,
            Guid? notificationScheduledJobId = null)
        {
            return new NotificationLog
            {
                NotificationLogId = Uuid7.NewUuid7(),
                DeviceInfoId = device.DeviceInfoId,
                NotificationScheduledJobId = notificationScheduledJobId,
                NotificationSource = source,
                Gw = device.Gw,
                Title = title,
                Body = body,
                NotificationStatus = status,
                ResultMsg = msg,
                RetryCount = 0,
                CreateAtUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 查詢推播目標裝置（依 DeviceIds、NotificationGroup、全部）
        /// </summary>
        private async Task<List<DeviceInfo>> GetTargetDevicesAsync(NotificationRequestDto request)
        {
            if (request.DeviceInfoIds != null && request.DeviceInfoIds.Any())
            {
                return await _db.DeviceInfos
                    .Where(x => request.DeviceInfoIds.Contains(x.DeviceInfoId) && x.Status)
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
        /// 依裝置分批，每批 size = {batchSize}，可用於限流與效能最佳化
        /// </summary>
        private IEnumerable<List<T>> Batch<T>(List<T> source, int batchSize)
        {
            for (int i = 0; i < source.Count; i += batchSize)
                yield return source.GetRange(i, Math.Min(batchSize, source.Count - i));
        }

        #endregion private methods
    }
}
