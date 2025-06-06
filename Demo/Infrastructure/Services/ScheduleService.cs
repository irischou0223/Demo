using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services
{
    public class ScheduleService
    {
        private readonly DemoDbContext _db;
        private readonly NotificationService _notificationService;
        private readonly ILogger<ScheduleService> _logger;

        public ScheduleService(
            DemoDbContext db,
            NotificationService notificationService,
            ILogger<ScheduleService> logger)
        {
            _db = db;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Hangfire排程主入口
        /// </summary>
        public async Task ExecuteScheduledJobsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var jobs = await _db.NotificationScheduledJobs
                    .Where(j => j.IsEnabled && j.NextRunAtUtc != null && j.NextRunAtUtc <= now)
                    .ToListAsync();

                foreach (var job in jobs)
                {
                    try
                    {
                        // Step 1: 透過 notification_msg_template_id 取得範本與語系/標題/內容
                        var template = await _db.NotificationMsgTemplates
                            .FirstOrDefaultAsync(t => t.NotificationMsgTemplateId == job.NotificationMsgTemplateId);
                        if (template == null)
                        {
                            _logger.LogWarning("找不到 NotificationMsgTemplate: {TemplateId}", job.NotificationMsgTemplateId);
                            continue;
                        }

                        // Step 1b: 查 CodeInfo
                        var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(c => c.CodeInfoId == template.CodeInfoId);
                        if (codeInfo == null)
                        {
                            _logger.LogWarning("找不到 CodeInfo: {CodeInfoId}", template.CodeInfoId);
                            continue;
                        }

                        // Step 2: 查 template data
                        var templateDataList = await _db.NotificationMsgTemplateDatas
                            .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                            .ToListAsync();
                        var templateData = templateDataList.ToDictionary(d => d.Key, d => d.Value);

                        // Step 3: 依 NotificationScope 及 NotificationTarget/NotificationGroup 決定發送對象
                        List<string> deviceIds = null;
                        string notificationGroup = null;
                        switch (job.NotificationScope)
                        {
                            case 1: // single
                                if (job.NotificationTarget != null && job.NotificationTarget.Any())
                                    deviceIds = job.NotificationTarget.Select(g => g.ToString()).ToList();
                                break;
                            case 2: // group
                                if (job.NotificationGroup != null && job.NotificationGroup.Any())
                                    notificationGroup = job.NotificationGroup.First();
                                break;
                            case 3: // all
                            default:
                                // 不指定目標，Service 會自動發給全體
                                break;
                        }

                        // Step 4: 組 NotificationRequestDto
                        var request = new NotificationRequestDto
                        {
                            DeviceIds = deviceIds,
                            NotificationGroup = notificationGroup,
                            Code = codeInfo.Code, // 可供策略用，但下方直接傳入標題內容等
                            Lang = codeInfo.Lang
                        };

                        var result = await _notificationService.NotifyByTargetAsync(
                            request,
                            codeInfo.Title ?? "",
                            codeInfo.Body ?? "",
                            template,
                            templateData
                        );

                        // 可選：寫入 JobNotificationLog
                        var log = new JobNotificationLog
                        {
                            JobNotificationLogId = Guid.NewGuid(),
                            NotificationScheduledJobId = job.NotificationScheduledJobId,
                            DeviceInfoId = deviceIds != null && deviceIds.Any() ? Guid.Parse(deviceIds.First()) : Guid.Empty,
                            Gw = template.Gw,
                            Title = codeInfo.Title ?? "",
                            Body = codeInfo.Body ?? "",
                            NotificationStatus = result.IsSuccess,
                            ResultMsg = result.Message,
                            RetryCount = 0,
                            CreateAtUtc = DateTime.UtcNow
                        };
                        _db.JobNotificationLogs.Add(log);

                        if (!result.IsSuccess)
                        {
                            _logger.LogWarning("排程推播失敗: JobId={JobId}, Msg={Msg}", job.NotificationScheduledJobId, result.Message);
                        }
                        else
                        {
                            _logger.LogInformation("排程推播成功: JobId={JobId}, Devices={Count}", job.NotificationScheduledJobId, deviceIds?.Count ?? 0);
                        }

                        // 更新下次執行時間
                        job.NextRunAtUtc = CalcNextRunTime(job.ScheduleType, job.NextRunAtUtc ?? job.ScheduleTime);
                        _db.NotificationScheduledJobs.Update(job);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "單一排程推播任務執行失敗 JobId={JobId}", job.NotificationScheduledJobId);
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteScheduledJobsAsync 全域例外");
            }
        }

        /// <summary>
        /// 計算下次執行時間
        /// </summary>
        private DateTime? CalcNextRunTime(short scheduleType, DateTime from)
        {
            // scheduleType: 0=immediate, 1=daily, 2=monthly, 3=yearly, 4=custom
            switch (scheduleType)
            {
                case 0: // immediate
                    return null;
                case 1: // daily
                    return from.AddDays(1);
                case 2: // monthly
                    return from.AddMonths(1);
                case 3: // yearly
                    return from.AddYears(1);
                default:
                    return from.AddDays(1); // custom 可依需求擴充
            }
        }
    }
}
