using Microsoft.AspNetCore.Mvc;

namespace PPMEnergyWeb.Controllers
{
    public class AboutController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}