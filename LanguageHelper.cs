using Microsoft.AspNetCore.Mvc;

namespace HayataAtilmaFormu.Helpers
{
    public static class LanguageHelper
    {
        // Basit sözlük sistemi
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            ["tr"] = new Dictionary<string, string>
            {
                ["LoginTitle"] = "Giriş Yap",
                ["SystemLogin"] = "Sisteme Giriş",
                ["StudentNoOrEmail"] = "Öğrenci No / E-posta",
                ["StudentNoOrEmailPlaceholder"] = "Öğrenci numaranızı veya e-posta adresinizi girin",
                ["Password"] = "Şifre",
                ["EnterPassword"] = "Şifrenizi girin",
                ["LoginButton"] = "Giriş Yap",
                ["LoggingIn"] = "Giriş yapılıyor",
                ["Students"] = "Öğrenciler",
                ["EnterStudentNo"] = "Öğrenci numaranızı girin",
                ["Authorities"] = "Yetkililer",
                ["EnterEmailAddress"] = "E-posta adresinizi girin",
                ["NoAccountYet"] = "Henüz hesabınız yok mu?",
                ["StudentRegistration"] = "Öğrenci Kaydı",
                ["AuthorityRegistration"] = "Yetkili Kaydı",
                ["ForgotPassword"] = "Şifremi Unuttum",
                ["LoginWithStudentNo"] = "Öğrenci numaranızla giriş yapın",
                ["LoginWithEmail"] = "E-posta adresinizle giriş yapın"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["LoginTitle"] = "Login",
                ["SystemLogin"] = "System Login",
                ["StudentNoOrEmail"] = "Student No / Email",
                ["StudentNoOrEmailPlaceholder"] = "Enter your student number or email address",
                ["Password"] = "Password",
                ["EnterPassword"] = "Enter your password",
                ["LoginButton"] = "Login",
                ["LoggingIn"] = "Logging in",
                ["Students"] = "Students",
                ["EnterStudentNo"] = "Enter your student number",
                ["Authorities"] = "Authorities",
                ["EnterEmailAddress"] = "Enter your email address",
                ["NoAccountYet"] = "Don't have an account yet?",
                ["StudentRegistration"] = "Student Registration",
                ["AuthorityRegistration"] = "Authority Registration",
                ["ForgotPassword"] = "Forgot Password",
                ["LoginWithStudentNo"] = "Login with your student number",
                ["LoginWithEmail"] = "Login with your email address",
            }
        };

        public static string GetText(string key, HttpContext httpContext)
        {
            // Mevcut dili al
            var lang = GetCurrentLanguage(httpContext);

            // Çeviriyi bul
            if (Translations.ContainsKey(lang) && Translations[lang].ContainsKey(key))
            {
                return Translations[lang][key];
            }

            // Bulamazsa Türkçe'yi dene
            if (Translations["tr"].ContainsKey(key))
            {
                return Translations["tr"][key];
            }

            // O da yoksa key'i döndür
            return key;
        }

        public static string GetCurrentLanguage(HttpContext httpContext)
        {
            // Önce session'dan bak
            var lang = httpContext.Session.GetString("Language");

            // Session'da yoksa cookie'den bak
            if (string.IsNullOrEmpty(lang))
            {
                httpContext.Request.Cookies.TryGetValue("Language", out lang);
            }

            // Hiçbir yerde yoksa varsayılan Türkçe
            if (string.IsNullOrEmpty(lang) || (lang != "tr" && lang != "en"))
            {
                lang = "tr";
            }

            return lang;
        }
    }

    // View'larda kullanım için extension
    public static class ViewContextExtensions
    {
        public static string T(this Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper html, string key)
        {
            return LanguageHelper.GetText(key, html.ViewContext.HttpContext);
        }
    }
}