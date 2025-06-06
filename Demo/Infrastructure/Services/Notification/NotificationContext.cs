using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services.Notification
{
    public class NotificationContext
    {
        private readonly DemoDbContext _db;
        private readonly AppNotificationStrategy _appStrategy;
        private readonly WebNotificationStrategy _webStrategy;
        private readonly EmailNotificationStrategy _emailStrategy;
        private readonly LineNotificationStrategy _lineStrategy;

        public NotificationContext(DemoDbContext db, AppNotificationStrategy appStrategy, WebNotificationStrategy webStrategy, EmailNotificationStrategy emailStrategy, LineNotificationStrategy lineStrategy)
        {
            _db = db;
            _appStrategy = appStrategy;
            _webStrategy = webStrategy;
            _emailStrategy = emailStrategy;
            _lineStrategy = lineStrategy;
        }

        public async Task<NotificationResultResponse> NotifyByTargetAsync(NotificationRequestDto request)
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
            var lang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : (devices.FirstOrDefault()?.Lang ?? "zh-TW");

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

            // 6. 查各裝置推播啟用類型（NotificationType）
            var deviceGuidList = devices.Select(d => d.DeviceInfoId).ToList();
            var notificationTypes = await _db.NotificationTypes
                .Where(x => deviceGuidList.Contains(x.DeviceInfoId))
                .ToDictionaryAsync(x => x.DeviceInfoId);

            // 7. 依推播方式分群
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

            var title = codeInfo.Title ?? "";
            var message = codeInfo.Body ?? "";

            var tasks = new List<Task>();

            // App/Web 分組依產品推播
            if (appDevices.Any())
            {
                foreach (var group in appDevices.GroupBy(d => d.ProductInfoId))
                {
                    var productDevices = group.ToList();
                    tasks.Add(_appStrategy.SendAsync(productDevices, title, message, customData, template));
                }
            }

            if (webDevices.Any())
            {
                foreach (var group in webDevices.GroupBy(d => d.ProductInfoId))
                {
                    var productDevices = group.ToList();
                    tasks.Add(_webStrategy.SendAsync(productDevices, title, message, customData, template));
                }
            }

            // Email/Line 分批
            if (emailDevices.Any())
                tasks.Add(_emailStrategy.SendAsync(emailDevices, title, message, customData, template));

            if (lineDevices.Any())
                tasks.Add(_lineStrategy.SendAsync(lineDevices, title, message, customData, template));

            await Task.WhenAll(tasks);

            result.IsSuccess = true;
            result.Message = $"推播已送出，共 {devices.Count} 台裝置";
            return result;
        }
    }
}
