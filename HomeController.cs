using System.Diagnostics;
using HayataAtilmaFormu.Models;
using Microsoft.AspNetCore.Mvc;

namespace HayataAtilmaFormu.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Eðer kullanýcý zaten giriþ yapmýþsa, rolüne göre yönlendir
            var userType = HttpContext.Session.GetString("UserType");
            var userId = HttpContext.Session.GetString("UserId");

            if (!string.IsNullOrEmpty(userType) && !string.IsNullOrEmpty(userId))
            {
                return userType switch
                {
                    "Ogrenci" => RedirectToAction("Index", "Ogrenci"),
                    "Yetkili" => RedirectToAction("Index", "Yetkili"),
                    "SuperAdmin" => RedirectToAction("Index", "Admin"),
                    _ => View()
                };
            }

            // Giriþ yapmamýþ kullanýcýlar için ana sayfa
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}