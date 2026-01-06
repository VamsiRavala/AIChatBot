using Microsoft.AspNetCore.Mvc;

namespace AIChatBot.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
