using Demo.Infrastructure.Services;
using Demo.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationContext;

        public NotificationController(NotificationService notificationContext)
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
