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
        private readonly AppNotificationStrategy _appStrategy;
        private readonly WebNotificationStrategy _webStrategy;
        private readonly EmailNotificationStrategy _emailStrategy;
        private readonly LineNotificationStrategy _lineStrategy;
        private readonly NotificationQueueService _queueService;

        public NotificationService(DemoDbContext db, AppNotificationStrategy appStrategy, WebNotificationStrategy webStrategy, EmailNotificationStrategy emailStrategy, LineNotificationStrategy lineStrategy, NotificationQueueService queueService)
        {
            _db = db;
            _appStrategy = appStrategy;
            _webStrategy = webStrategy;
            _emailStrategy = emailStrategy;
            _lineStrategy = lineStrategy;
            _queueService = queueService;
        }

        /// <summary>
        /// 推播主要入口，可選擇直接推播或進queue。
        /// </summary>
        public async Task<NotificationResultResponse> NotifyAsync(
            NotificationRequestDto request,
            NotificationSourceType source,
            bool useQueue = false)
        {
            if (useQueue)
            {
                await _queueService.EnqueueAsync(request, source);
                return new NotificationResultResponse { IsSuccess = true, Message = "已加入推播佇列" };
            }
            else
            {
                return await NotifyByTargetAsync(request, source);
            }
        }



        /// <summary>
        /// 依據條件查詢裝置，組合推播資料後推播（同步）
        /// </summary>
        public async Task<NotificationResultResponse> NotifyByTargetAsync(NotificationRequestDto request, NotificationSourceType source)
        {
            var result = new NotificationResultResponse();

            // 1. 查詢目標裝置
            List<DeviceInfo> devices;
            if (request.DeviceIds != null && request.DeviceIds.Any())
            {
                devices = await _db.DeviceInfos
                    .Where(x => request.DeviceIds.Contains(x.DeviceId) && x.Status)
                    .ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(request.NotificationGroup))
            {
                devices = await _db.DeviceInfos
                    .Where(x => x.NotificationGroup == request.NotificationGroup && x.Status)
                    .ToListAsync();
            }
            else
            {
                devices = await _db.DeviceInfos
                    .Where(x => x.Status)
                    .ToListAsync();
            }

            if (!devices.Any())
            {
                result.IsSuccess = false;
                result.Message = "查無推播目標裝置";
                return result;
            }

            // 2. 查語系，預設 zh-TW
            var lang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : devices.FirstOrDefault()?.Lang ?? "zh-TW";

            // 3. 查 CodeInfo
            var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(x => x.Code == request.Code && x.Lang == lang);
            if (codeInfo == null)
            {
                result.IsSuccess = false;
                result.Message = "查無對應通知代碼";
                return result;
            }

            // 4. 查 NotificationMsgTemplate
            var gw = devices.FirstOrDefault()?.Gw;
            var template = await _db.NotificationMsgTemplates
                .Where(x => x.CodeInfoId == codeInfo.CodeInfoId && x.Gw == gw)
                .FirstOrDefaultAsync();

            if (template == null)
            {
                template = await _db.NotificationMsgTemplates
                    .Where(x => x.CodeInfoId == codeInfo.CodeInfoId)
                    .FirstOrDefaultAsync();
            }

            if (template == null)
            {
                result.IsSuccess = false;
                result.Message = "查無訊息模板";
                return result;
            }

            // 5. 查 NotificationMsgTemplateData
            var customDataList = await _db.NotificationMsgTemplateDatas
                .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                .ToListAsync();
            var customData = customDataList.ToDictionary(d => d.Key, d => d.Value);

            // 6. 推播
            var innerResult = await NotifyByTargetAsyncInternal(
                request,
                devices,
                codeInfo.Title ?? "",
                codeInfo.Body ?? "",
                template,
                customData,
                source);

            // 7. Log寫入
            foreach (var device in devices)
            {
                WriteNotificationLog(source, device, codeInfo.Title ?? "", codeInfo.Body ?? "", innerResult.IsSuccess, innerResult.Message);
            }
            await _db.SaveChangesAsync();

            return innerResult;
        }

        /// <summary>
        /// 直接指定內容與範本推播（給排程等用）
        /// </summary>
        public async Task<NotificationResultResponse> NotifyByTargetAsync(
            NotificationRequestDto request,
            string title,
            string body,
            NotificationMsgTemplate template,
            Dictionary<string, string> templateData,
            NotificationSourceType source)
        {
            var result = new NotificationResultResponse();

            // 查詢目標裝置
            List<DeviceInfo> devices;
            if (request.DeviceIds != null && request.DeviceIds.Any())
            {
                devices = await _db.DeviceInfos
                    .Where(x => request.DeviceIds.Contains(x.DeviceId) && x.Status)
                    .ToListAsync();
            }
            else if (!string.IsNullOrWhiteSpace(request.NotificationGroup))
            {
                devices = await _db.DeviceInfos
                    .Where(x => x.NotificationGroup == request.NotificationGroup && x.Status)
                    .ToListAsync();
            }
            else
            {
                devices = await _db.DeviceInfos
                    .Where(x => x.Status)
                    .ToListAsync();
            }

            if (!devices.Any())
            {
                result.IsSuccess = false;
                result.Message = "查無推播目標裝置";
                return result;
            }

            var innerResult = await NotifyByTargetAsyncInternal(
                request,
                devices,
                title,
                body,
                template,
                templateData,
                source);

            foreach (var device in devices)
            {
                WriteNotificationLog(source, device, title, body, innerResult.IsSuccess, innerResult.Message);
            }
            await _db.SaveChangesAsync();

            return innerResult;
        }

        /// <summary>
        /// 給RetryService用，根據失敗Log重發
        /// </summary>
        public async Task<bool> RetrySendNotificationAsync(ExternalNotificationLog log)
        {
            var device = await _db.DeviceInfos.FirstOrDefaultAsync(x => x.DeviceInfoId == log.DeviceInfoId && x.Status);
            if (device == null) return false;

            // 你可以依照實際需求查模板、CodeInfo等或直接用log的內容
            try
            {
                var customData = new Dictionary<string, string>(); // 若有紀錄可帶入
                                                                   // 這裡假設是 App 推播, 可依需求調整
                await _appStrategy.SendAsync(new List<DeviceInfo> { device }, log.Title, log.Body, customData, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region private methods
        /// <summary>
        /// 依推播方式分類推播，呼叫各策略
        /// </summary>
        private async Task<NotificationResultResponse> NotifyByTargetAsyncInternal(
            NotificationRequestDto request,
            List<DeviceInfo> devices,
            string title,
            string body,
            NotificationMsgTemplate template,
            Dictionary<string, string> customData,
            NotificationSourceType source)
        {
            var result = new NotificationResultResponse();

            // 查各裝置推播啟用類型（NotificationType）
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

            var tasks = new List<Task>();

            // App/Web分組依產品推播
            if (appDevices.Any())
            {
                foreach (var group in appDevices.GroupBy(d => d.ProductInfoId))
                {
                    var productDevices = group.ToList();
                    tasks.Add(_appStrategy.SendAsync(productDevices, title, body, customData, template));
                }
            }
            if (webDevices.Any())
            {
                foreach (var group in webDevices.GroupBy(d => d.ProductInfoId))
                {
                    var productDevices = group.ToList();
                    tasks.Add(_webStrategy.SendAsync(productDevices, title, body, customData, template));
                }
            }
            // Email/Line分批
            if (emailDevices.Any())
                tasks.Add(_emailStrategy.SendAsync(emailDevices, title, body, customData, template));
            if (lineDevices.Any())
                tasks.Add(_lineStrategy.SendAsync(lineDevices, title, body, customData, template));

            await Task.WhenAll(tasks);

            result.IsSuccess = true;
            result.Message = $"推播已送出，共 {devices.Count} 台裝置";
            return result;
        }


        /// <summary>
        /// 依來源分類寫Log
        /// </summary>
        private void WriteNotificationLog(NotificationSourceType source, DeviceInfo device, string title, string body, bool status, string msg)
        {
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
                        CreateAtUtc = DateTime.UtcNow
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
                        CreateAtUtc = DateTime.UtcNow
                    });
                    break;
                    // Job 請於 ScheduleService 實作
            }
        }

        #endregion private methods
    }
}
