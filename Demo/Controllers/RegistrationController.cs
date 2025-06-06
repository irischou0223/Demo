using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers
{
    public class RegistrationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
