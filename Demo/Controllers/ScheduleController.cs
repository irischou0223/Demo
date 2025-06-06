using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    public class ScheduleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
