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

        /// <summary>
        /// 通知主入口，支援自動判斷是否進 queue，來源由外部指定
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("Notify")]
        public async Task<IActionResult> Notify([FromBody] NotificationRequestDto request)
        {
            // 若裝置數大於 1000 則進 queue
            var useQueue = request.DeviceIds?.Count > 1000;
            var source = NotificationSourceType.Backend; // 依實際需求設置

            var result = await _notificationService.NotifyAsync(request, request.Source, useQueue);

            if (result.IsSuccess)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
    }
}
