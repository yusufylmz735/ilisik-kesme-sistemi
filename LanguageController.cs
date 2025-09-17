using Microsoft.AspNetCore.Mvc;

namespace HayataAtilmaFormu.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult SetLanguage(string lang, string returnUrl)
        {
            // Sadece TR ve EN kabul et
            if (lang == "tr" || lang == "en")
            {
                // Session'a kaydet
                HttpContext.Session.SetString("Language", lang);

                // Cookie'ye de kaydet (kalıcı olsun)
                Response.Cookies.Append("Language", lang, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
            }

            // Geri dön
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }

        [HttpGet]
        public IActionResult GetCurrentLanguage()
        {
            // Önce session'dan bak
            var lang = HttpContext.Session.GetString("Language");

            // Session'da yoksa cookie'den bak
            if (string.IsNullOrEmpty(lang))
            {
                Request.Cookies.TryGetValue("Language", out lang);
            }

            // Hiçbir yerde yoksa varsayılan Türkçe
            if (string.IsNullOrEmpty(lang) || (lang != "tr" && lang != "en"))
            {
                lang = "tr";
            }

            return Json(new { lang = lang });
        }
    }
}