using Demo.Enum;
using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationContext)
        {
            _notificationService = notificationContext;
        }

        [HttpPost("Notify")]
        public async Task<IActionResult> Notify([FromBody] NotificationRequestDto request)
        {
            var isHighVolume = request.DeviceIds.Count > 1000; // 大量時才進Queue
            var useQueue = isHighVolume;
            var source = NotificationSourceType.Backend; // 依用戶端判斷

            var result = await _notificationService.NotifyAsync(request, source, useQueue);
            if (result.IsSuccess)
                return Ok(result);
            else
                return BadRequest(result);
        }
    }
}
