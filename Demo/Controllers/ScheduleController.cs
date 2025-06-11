using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
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
            _logger.LogInformation("[ ScheduleAPI ] 取得所有排程清單 開始");
            var jobs = await _db.NotificationScheduledJobs.ToListAsync();
            _logger.LogInformation("[ ScheduleAPI ] 取得所有排程清單 結束，共 {Count} 筆", jobs.Count);
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
            _logger.LogInformation("[ ScheduleAPI ] 取得單一排程 開始：{JobId}", id);
            var job = await _db.NotificationScheduledJobs.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("[ ScheduleAPI ] 查無排程：{JobId}", id);
                return NotFound();
            }
            _logger.LogInformation("[ ScheduleAPI ] 取得單一排程 結束：{JobId}", id);
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
            _logger.LogInformation("[ ScheduleAPI ] 新增排程 開始，標題：{Title}", req.Title);
            try
            {
                var entity = new NotificationScheduledJob
                {
                    NotificationScheduledJobId = Guid.NewGuid(),
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

                _logger.LogInformation("[ ScheduleAPI ] 新增排程 結束，成功建立：{JobId} 標題：{Title}", entity.NotificationScheduledJobId, entity.Title);
                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ScheduleAPI ] 新增排程失敗！");
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
            _logger.LogInformation("[ ScheduleAPI ] 修改排程 開始：{JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[ ScheduleAPI ] 查無排程：{JobId}", id);
                    return NotFound("找不到排程");
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
                _logger.LogInformation("[ ScheduleAPI ] 修改排程 結束：{JobId}", id);

                return Ok(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ScheduleAPI ] 修改排程失敗！");
                return BadRequest("修改排程失敗：" + ex.Message);
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
            _logger.LogInformation("[ ScheduleAPI ] 刪除排程 開始：{JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[ ScheduleAPI ] 查無排程：{JobId}", id);
                    return NotFound("找不到排程");
                }

                _db.NotificationScheduledJobs.Remove(job);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[ ScheduleAPI ] 刪除排程 結束：{JobId}", id);

                return Ok("刪除成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ScheduleAPI ] 刪除排程失敗！");
                return BadRequest("刪除排程失敗：" + ex.Message);
            }
        }

        /// <summary>
        /// 取消排程（僅標記，不移除資料）
        /// </summary>
        [HttpPost("cancel/{id}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            _logger.LogInformation("[ ScheduleAPI ] 取消排程 開始：{JobId}", id);
            try
            {
                var job = await _db.NotificationScheduledJobs.FirstOrDefaultAsync(x => x.NotificationScheduledJobId == id);
                if (job == null)
                {
                    _logger.LogWarning("[ ScheduleAPI ] 查無排程：{JobId}", id);
                    return NotFound("找不到排程");
                }

                job.IsEnabled = false;
                job.CancelledAtUtc = DateTime.UtcNow;
                job.UpdateAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                _logger.LogInformation("[ ScheduleAPI ] 取消排程 結束：{JobId}", id);

                return Ok("取消成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ ScheduleAPI ] 取消排程失敗！");
                return BadRequest("取消排程失敗：" + ex.Message);
            }
        }
    }
}
