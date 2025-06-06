using Demo.Infrastructure.Services.Notification;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationContext _notificationContext;

        public NotificationController(NotificationContext notificationContext)
        {
            _notificationContext = notificationContext;
        }

        [HttpPost("Notify")]
        public async Task<IActionResult> Notify([FromBody] NotificationRequestDto request)
        {
            var result = await _notificationContext.NotifyByTargetAsync(request);
            if (result.IsSuccess)
                return Ok(result);
            else
                return BadRequest(result);
        }
    }
}
