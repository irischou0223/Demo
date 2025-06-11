using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services.Notification;
using Demo.Models.DTOs;
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
        /// <param name="request">推播請求物件</param>
        /// <param name="source">推播來源（Backend/External）</param>
        /// <param name="useQueue">是否進 queue 非同步處理</param>
        /// <returns>推播結果</returns>
        public async Task<NotificationResponseDto> NotifyAsync(NotificationRequestDto request, NotificationSourceType source, bool useQueue = false)
        {
            _logger.LogInformation("[ NotificationService ] NotifyAsync 開始，來源: {Source}, useQueue: {UseQueue}, Request: {@Request}", source, useQueue, request);

            NotificationResponseDto result;
            if (useQueue)
            {
                // 進 queue 非同步發送
                _logger.LogInformation("[ NotificationService ] 推播即將加入佇列，來源: {Source}", source);
                await _queueService.EnqueueAsync(request, source);
                _logger.LogInformation("[ NotificationService ] 推播已加入佇列，來源: {Source}", source);
                result = new NotificationResponseDto { IsSuccess = true, Message = "已加入推播佇列" };
            }
            else
            {
                // 直接同步推播
                result = await NotifyByTargetAsync(request, source);
            }
            _logger.LogInformation("[ NotificationService ] NotifyAsync 結束，來源: {Source}, useQueue: {UseQueue}, IsSuccess: {IsSuccess}, Message: {Message}", source, useQueue, result.IsSuccess, result.Message);
            return result;
        }

        /// <summary>
        /// 依據條件查詢裝置，組合推播資料並發送
        /// 1. 查詢目標裝置
        /// 2. 決定推播內容來源（templateId 或 code）
        /// 3. 分流調用推播
        /// </summary>
        public async Task<NotificationResponseDto> NotifyByTargetAsync(NotificationRequestDto request, NotificationSourceType source)
        {
            _logger.LogInformation("[ NotificationService ] NotifyByTargetAsync 開始，來源: {Source}, Request: {@Request}", source, request);

            var result = new NotificationResponseDto();

            // 1. 查詢目標裝置
            var devices = await GetTargetDevicesAsync(request);
            _logger.LogInformation("[ NotificationService ] NotifyByTargetAsync 查詢到裝置數量: {DeviceCount}", devices.Count);

            if (!devices.Any())
            {
                result.IsSuccess = false;
                result.Message = "查無推播目標裝置";
                _logger.LogWarning("[ NotificationService ] NotifyByTargetAsync 結束，查無推播目標裝置");
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
                _logger.LogInformation("[ NotificationService ] NotifyByTargetAsync 使用 templateId: {TemplateId}", request.NotificationMsgTemplateId);
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
                // [補充說明] 以 code 查找標準訊息模板，常用於依規格定義的訊息通知
                _logger.LogInformation("[ NotificationService ] NotifyByTargetAsync 使用 code: {Code}", request.Code);
                var lang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : devices.FirstOrDefault()?.Lang ?? "zh-TW";
                var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(x => x.Code == request.Code && x.Lang == lang);
                if (codeInfo == null)
                {
                    result.IsSuccess = false;
                    result.Message = "查無對應通知代碼";
                    _logger.LogWarning("[ NotificationService ] NotifyByTargetAsync 結束，查無對應通知代碼");
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
                    _logger.LogWarning("[ NotificationService ] NotifyByTargetAsync 結束，查無訊息模板");
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
            result = await NotifyByTargetAsyncInternal(devices, title, body, template, customData, source);

            _logger.LogInformation("[ NotificationService ] NotifyByTargetAsync 結束，來源: {Source}, IsSuccess: {IsSuccess}, Message: {Message}", source, result.IsSuccess, result.Message);

            return result;
        }

        /// <summary>
        /// 根據失敗 Log 直接重發（只用 log 內容）
        /// </summary>
        public async Task<bool> RetrySendNotificationAsync(object log, bool isBackend = false)
        {
            _logger.LogInformation("[ NotificationService ] RetrySendNotificationAsync 開始, isBackend: {IsBackend}", isBackend);

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
                _logger.LogWarning("[ NotificationService ] RetrySendNotificationAsync: Log型別不正確");
                return false;
            }

            // 2. 已發送成功則不重發
            if (notificationStatus)
            {
                _logger.LogInformation("[ NotificationService ] RetrySendNotificationAsync 結束，無需重發，已標示成功");
                return true;
            }

            // 3. 查裝置
            var device = await _db.DeviceInfos.FirstOrDefaultAsync(x => x.DeviceInfoId == deviceInfoId && x.Status);
            if (device == null)
            {
                _logger.LogWarning("[ NotificationService ] RetrySendNotificationAsync 結束，查無裝置 DeviceInfoId={DeviceInfoId}", deviceInfoId);
                return false;
            }

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

            _logger.LogInformation("[ NotificationService ] RetrySendNotificationAsync 結束, isBackend: {IsBackend}, 成功: {IsSuccess}", isBackend, isSuccess);
            return isSuccess;
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
            bool writeLog = true)
        {
            _logger.LogInformation("[ NotificationService ] NotifyByTargetAsyncInternal 開始，裝置數量: {DeviceCount}, Title: {Title}, Source: {Source}", devices.Count, title, source);

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
                        _logger.LogInformation("[ NotificationService ] APP推播開始，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
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
                            _logger.LogInformation("[ NotificationService ] APP推播結束，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref appFailed, 1);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"App推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "[ NotificationService ] App推播失敗，ProductInfoId: {ProductInfoId}", group.Key);
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
                        _logger.LogInformation("[ NotificationService ] Web推播開始，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        try
                        {
                            await _webStrategy.SendAsync(batch, title, body, customData, template);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Web推播已發送");
                                }
                            }
                            _logger.LogInformation("[ NotificationService ] Web推播結束，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref webFailed, 1);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Web推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "[ NotificationService ] Web推播失敗，ProductInfoId: {ProductInfoId}", group.Key);
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
                        _logger.LogInformation("[ NotificationService ] Email推播開始，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        try
                        {
                            await _emailStrategy.SendAsync(batch, title, body, customData, template);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Email推播已發送");
                                }
                            }
                            _logger.LogInformation("[ NotificationService ] Email推播結束，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref emailFailed, 1);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Email推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "[ NotificationService ] Email推播失敗，ProductInfoId: {ProductInfoId}", group.Key);
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
                        _logger.LogInformation("[ NotificationService ] Line推播開始，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        try
                        {
                            await _lineStrategy.SendAsync(batch, title, body, customData, template);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, true, "Line推播已發送");
                                }
                            }
                            _logger.LogInformation("[ NotificationService ] Line推播結束，批次裝置數: {BatchCount}, ProductInfoId: {ProductInfoId}", batch.Count, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref lineFailed, 1);
                            if (writeLog)
                            {
                                foreach (var device in batch)
                                {
                                    WriteNotificationLog(source, device, title, body, false, $"Line推播失敗: {ex.Message}");
                                }
                            }
                            _logger.LogError(ex, "[ NotificationService ] Line推播失敗，ProductInfoId: {ProductInfoId}", group.Key);
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
            {
                result.Message = $"推播已送出，共 {devices.Count} 台裝置";
            }
            else
            {
                result.Message = $"推播部份通道({string.Join(",", failList)})有失敗，共 {devices.Count} 台裝置";
            }

            _logger.LogInformation("[ NotificationService ] NotifyByTargetAsyncInternal 結束，裝置數量: {DeviceCount}, IsSuccess: {IsSuccess}, Message: {Message}", devices.Count, result.IsSuccess, result.Message);

            return result;
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

        /// <summary>
        /// 新增推播發送記錄
        /// </summary>
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
