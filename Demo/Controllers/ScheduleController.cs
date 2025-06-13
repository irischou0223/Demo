using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Medo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers
{
    /// <summary>
    /// 通知排程管理 API
    /// 提供排程任務的新增、修改、刪除、取消等操作。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly DemoDbContext _db;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(DemoDbContext db, ILogger<ScheduleController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 取得所有排程清單
        /// </summary>
        /// <returns>所有排程任務</returns>
        [HttpGet("list")]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("[Schedule] Querying all scheduled jobs.");
            var jobs = await _db.NotificationScheduledJobs.ToListAsync();
            _logger.LogInformation("[Schedule] Successfully retrieved all scheduled jobs. Count: {Count}", jobs.Count);
            return Ok(jobs);
        }

        /// <summary>
        /// 取得單一排程
        /// </summary>
        /// <param name="id">排程主鍵</param>
        /// <returns>指定排程任務</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            _logger.LogInformation("[Schedule] Querying scheduled job. JobId: {JobId}", id);
            var job = await _db.NotificationScheduledJobs.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("[Schedule] Scheduled job not found. JobId: {JobId}", id);
                return NotFound();
            }
            _logger.LogInformation("[Schedule] Successfully retrieved scheduled job. JobId: {JobId}", id);
            return Ok(job);
        }

        /// <summary>
        /// 新增排程
        /// </summary>
        /// <param name="req">排程請求 DTO</param>
        /// <returns>新增後排程資料</returns>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] NotificationScheduledJobRequestDto req)
        {
            _logger.LogInformation("[Schedule] Creating new scheduled job. Title: {Title}, NotificationMsgTemplateId: {TemplateId}", req.Title, req.NotificationMsgTemplateId);
            try
            {
                var entity = new NotificationScheduledJob
                {
                    NotificationScheduledJobId = Uuid7.NewUuid7(),
                    NotificationMsgTemplateId = req.NotificationMsgTemplateId,
                    Title = req.Title,
                    NotificationScope = req.NotificationScope,
                    NotificationTarget = req.NotificationTarget,
                    NotificationGroup = req.NotificationGroup,
                    ScheduleFrequencyType = req.ScheduleFrequencyType,
                    ScheduleTime = req.ScheduleTime ?? DateTime.UtcNow,
                    NotificationChannelType = req.NotificationChannelType,
                    IsEnabled = true,
                    NextRunAtUtc = req.NextRunAtUtc,
                    CreateAtUtc = DateTime.UtcNow,
                    UpdateAtUtc = null,
                    CancelledAtUtc = null,
                };

                _db.NotificationScheduledJobs.Add(entity);
                await _db.SaveChangesAsync();

                _logger.LogInformation("[Schedule] Scheduled job created successfully. JobId: {JobId}, Title: {Title}", entity.NotificationScheduledJobId, entity.Title);
                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Schedule] Failed to create scheduled job. Title: {Title}", req.Title);
                return BadRequest("新增排程失敗：" + ex.Message);
            }
        }

        /// <summary>
        /// 修改排程
        /// </summary>
        /// <param name="id">排程主鍵</param>
        /// <param name="req">排程請求 DTO</param>
        /// <returns>修改後排程資料</returns>
        [HttpPost("update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] NotificationScheduledJob req)
        {
            _logger.LogInformation("[Schedule] Updating scheduled job. JobId: {JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[Schedule] Scheduled job not found for update. JobId: {JobId}", id);
                    return NotFound(new { message = "Scheduled job not found." });
                }

                job.NotificationMsgTemplateId = req.NotificationMsgTemplateId;
                job.Title = req.Title;
                job.NotificationScope = req.NotificationScope;
                job.NotificationTarget = req.NotificationTarget;
                job.NotificationGroup = req.NotificationGroup;
                job.ScheduleFrequencyType = req.ScheduleFrequencyType;
                job.ScheduleTime = req.ScheduleTime;
                job.NotificationChannelType = req.NotificationChannelType;
                job.IsEnabled = req.IsEnabled;
                job.NextRunAtUtc = req.NextRunAtUtc;
                job.UpdateAtUtc = DateTime.UtcNow;
                job.CancelledAtUtc = null;

                await _db.SaveChangesAsync();
                _logger.LogInformation("[Schedule] Scheduled job updated successfully. JobId: {JobId}", id);

                return Ok(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Schedule] Failed to update scheduled job. JobId: {JobId}", id);
                return BadRequest(new { message = "Failed to update scheduled job: " + ex.Message });
            }
        }

        /// <summary>
        /// 刪除排程
        /// </summary>
        /// <param name="id">排程主鍵</param>
        /// <returns>刪除成功訊息</returns>
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            _logger.LogInformation("[Schedule] Deleting scheduled job. JobId: {JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[Schedule] Scheduled job not found for delete. JobId: {JobId}", id);
                    return NotFound(new { message = "Scheduled job not found." });
                }

                _db.NotificationScheduledJobs.Remove(job);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[Schedule] Scheduled job deleted successfully. JobId: {JobId}", id);

                return Ok(new { message = "Deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Schedule] Failed to delete scheduled job. JobId: {JobId}", id);
                return BadRequest(new { message = "Failed to delete scheduled job: " + ex.Message });
            }
        }

        /// <summary>
        /// 取消排程（僅標記，不移除資料）
        /// </summary>
        [HttpPost("cancel/{id}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            _logger.LogInformation("[Schedule] Cancelling scheduled job. JobId: {JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[Schedule] Scheduled job not found for cancel. JobId: {JobId}", id);
                    return NotFound(new { message = "Scheduled job not found." });
                }

                job.IsEnabled = false;
                job.CancelledAtUtc = DateTime.UtcNow;
                job.UpdateAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                _logger.LogInformation("[Schedule] Scheduled job cancelled successfully. JobId: {JobId}", id);

                return Ok(new { message = "Cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Schedule] Failed to cancel scheduled job. JobId: {JobId}", id);
                return BadRequest(new { message = "Failed to cancel scheduled job: " + ex.Message });
            }
        }
    }
}
