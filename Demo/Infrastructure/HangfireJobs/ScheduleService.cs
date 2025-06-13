using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Demo.Infrastructure.Hangfire
{
    /// <summary>
    /// 通知排程服務（ScheduleService）
    /// ---
    /// 依據排程規則，定時批次執行通知推播，
    /// 並於每筆任務執行後記錄結果與下次執行時間。
    /// </summary>
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
            _logger.LogInformation("Scheduled notification job execution started.");
            var sw = Stopwatch.StartNew();
            int totalProcessed = 0;

            try
            {
                var now = DateTime.UtcNow;

                // 1. 取得待執行的排程
                var jobs = await _db.NotificationScheduledJobs
                    .Where(j => j.IsEnabled && j.NextRunAtUtc != null && j.NextRunAtUtc <= now)
                    .ToListAsync();

                _logger.LogInformation("Number of jobs to execute: {JobCount}", jobs.Count);

                foreach (var job in jobs)
                {
                    try
                    {
                        _logger.LogInformation("Executing scheduled job. JobId={JobId}", job.NotificationScheduledJobId);

                        // 2. 查模板
                        var template = await _db.NotificationMsgTemplates
                            .FirstOrDefaultAsync(t => t.NotificationMsgTemplateId == job.NotificationMsgTemplateId);
                        if (template == null)
                        {
                            _logger.LogWarning("NotificationMsgTemplate not found. TemplateId={TemplateId}", job.NotificationMsgTemplateId);
                            continue;
                        }

                        // 3. 查 CodeInfo
                        var codeInfo = await _db.CodeInfos.FirstOrDefaultAsync(c => c.CodeInfoId == template.CodeInfoId);
                        if (codeInfo == null)
                        {
                            _logger.LogWarning("CodeInfo not found. CodeInfoId={CodeInfoId}", template.CodeInfoId);
                            continue;
                        }

                        // 4. 查自訂資料
                        var templateDataList = await _db.NotificationMsgTemplateDatas
                            .Where(x => x.NotificationMsgTemplateId == template.NotificationMsgTemplateId)
                            .ToListAsync();
                        var templateData = templateDataList.ToDictionary(d => d.Key, d => d.Value);

                        // 5. 決定推播目標
                        List<Guid> deviceInfoIds = null;
                        string notificationGroup = null;
                        switch (job.NotificationScope)
                        {
                            case NotificationScopeType.Single: // 單一裝置
                                if (job.NotificationTarget != null && job.NotificationTarget.Any())
                                    deviceInfoIds = job.NotificationTarget.Select(g => g).ToList();
                                break;
                            case NotificationScopeType.Group: // 群組
                                if (job.NotificationGroup != null && job.NotificationGroup.Any())
                                    notificationGroup = job.NotificationGroup.First();
                                break;
                            case NotificationScopeType.All: // 全部
                            default:
                                // 不指定目標，Service 會自動發給全體
                                break;
                        }

                        // 6. 組裝推播請求
                        var request = new NotificationRequestDto
                        {
                            Source = NotificationSourceType.Job,
                            DeviceInfoIds = deviceInfoIds,
                            NotificationGroup = notificationGroup,
                            Lang = codeInfo.Lang,
                            NotificationScheduledJobId = job.NotificationScheduledJobId,
                            NotificationMsgTemplateId = template.NotificationMsgTemplateId,
                            Title = codeInfo.Title,
                            Body = codeInfo.Body
                        };

                        // 7. 呼叫主推播服務（自動分流/分批、支援 queue）
                        var result = await _notificationService.NotifyAsync(request);

                        if (!result.IsSuccess)
                        {
                            _logger.LogWarning("Scheduled notification failed. JobId={JobId}, Message={Message}", job.NotificationScheduledJobId, result.Message);
                        }
                        else
                        {
                            _logger.LogInformation("Scheduled notification succeeded. JobId={JobId}, DeviceCount={DeviceCount}", job.NotificationScheduledJobId, deviceInfoIds?.Count ?? 0);
                        }

                        // 9. 更新下次執行時間
                        job.NextRunAtUtc = CalcNextRunTime(job.ScheduleFrequencyType, job.NextRunAtUtc ?? job.ScheduleTime);
                        _db.NotificationScheduledJobs.Update(job);
                        totalProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception in single scheduled job. JobId={JobId}", job.NotificationScheduledJobId);
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ExecuteScheduledJobsAsync");
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("End ExecuteScheduledJobsAsync. ProcessedJobCount={Count}, ElapsedMs={ElapsedMs}", totalProcessed, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// 計算下次執行時間
        /// </summary>
        private DateTime? CalcNextRunTime(ScheduleFrequencyType scheduleType, DateTime from)
        {
            switch (scheduleType)
            {
                case ScheduleFrequencyType.Immediate: // 立即
                    return null;
                case ScheduleFrequencyType.Daily: // 每日
                    return from.AddDays(1);
                case ScheduleFrequencyType.Monthly: // 每月
                    return from.AddMonths(1);
                case ScheduleFrequencyType.Yearly: // 每年
                    return from.AddYears(1);
                default:
                    return from.AddDays(1); // 其他可依需求擴充
            }
        }
    }
}
