using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Hangfire
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
        /// 排程主程序，依據條件逐筆執行推播
        /// </summary>
        public async Task ExecuteScheduledJobsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. 取得待執行的排程
                var jobs = await _db.NotificationScheduledJobs
                    .Where(j => j.IsEnabled && j.NextRunAtUtc != null && j.NextRunAtUtc <= now)
                    .ToListAsync();

                foreach (var job in jobs)
                {
                    try
                    {
                        // 2. 查模板
                        var template = await _db.NotificationMsgTemplates
                            .FirstOrDefaultAsync(t => t.NotificationMsgTemplateId == job.NotificationMsgTemplateId);
                        if (template == null)
                        {
                            _logger.LogWarning("找不到 NotificationMsgTemplate: {TemplateId}", job.NotificationMsgTemplateId);
                            continue;
                        }

                        // 3. 查 CodeInfo
                        var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(c => c.CodeInfoId == template.CodeInfoId);
                        if (codeInfo == null)
                        {
                            _logger.LogWarning("找不到 CodeInfo: {CodeInfoId}", template.CodeInfoId);
                            continue;
                        }

                        // 4. 查自訂資料
                        var templateDataList = await _db.NotificationMsgTemplateDatas
                            .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                            .ToListAsync();
                        var templateData = templateDataList.ToDictionary(d => d.Key, d => d.Value);

                        // 5. 決定推播目標
                        List<string> deviceIds = null;
                        string notificationGroup = null;
                        switch (job.NotificationScope)
                        {
                            case 1: // 單一裝置
                                if (job.NotificationTarget != null && job.NotificationTarget.Any())
                                    deviceIds = job.NotificationTarget.Select(g => g.ToString()).ToList();
                                break;
                            case 2: // 群組
                                if (job.NotificationGroup != null && job.NotificationGroup.Any())
                                    notificationGroup = job.NotificationGroup.First();
                                break;
                            case 3: // 全部
                            default:
                                // 不指定目標，Service 會自動發給全體
                                break;
                        }

                        // 6. 組裝推播請求
                        var request = new NotificationRequestDto
                        {
                            DeviceIds = deviceIds,
                            NotificationGroup = notificationGroup,
                            NotificationMsgTemplateId = template.NotificationMsgTemplateId,
                            Title = codeInfo.Title,
                            Body = codeInfo.Body,
                            Lang = codeInfo.Lang,
                            Source = NotificationSourceType.Job
                        };

                        // 7. 呼叫主推播服務（自動分流/分批、支援 queue）
                        var result = await _notificationService.NotifyAsync(request, request.Source);

                        // 8. 寫入排程通知Log
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

                        // 9. 更新下次執行時間
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
                case 0: // 立即
                    return null;
                case 1: // 每日
                    return from.AddDays(1);
                case 2: // 每月
                    return from.AddMonths(1);
                case 3: // 每年
                    return from.AddYears(1);
                default:
                    return from.AddDays(1); // 其他可依需求擴充
            }
        }
    }
}
