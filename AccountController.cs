using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;

namespace HayataAtilmaFormu.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Account/Login
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                var userType = HttpContext.Session.GetString("UserType");
                return userType switch
                {
                    ApplicationConstants.OgrenciRole => RedirectToAction("Index", "Ogrenci"),
                    ApplicationConstants.YetkiliRole => RedirectToAction("Index", "Yetkili"),
                    ApplicationConstants.SuperAdminRole => RedirectToAction("Index", "Admin"),
                    _ => RedirectToAction("Login")
                };
            }

            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string kullaniciAdi, string sifre)
        {
            if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrWhiteSpace(sifre))
            {
                ViewBag.Error = "Kullanıcı adı ve şifre boş bırakılamaz.";
                return View();
            }

            try
            {
                var hashedPassword = HashPassword(sifre);

                if (IsEmail(kullaniciAdi))
                {
                    return await HandleYetkiliLogin(kullaniciAdi, hashedPassword);
                }
                else
                {
                    return await HandleOgrenciLogin(kullaniciAdi, hashedPassword);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giriş sırasında hata oluştu.");
                ViewBag.Error = "Giriş sırasında bir hata oluştu.";
                return View();
            }
        }

        // GET: Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: Account/Register
        public async Task<IActionResult> Register()
        {
            await LoadRegisterData();
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Ogrenci ogrenci, string sifreTekrar)
        {
            try
            {
                if (ogrenci == null)
                {
                    ViewBag.Error = "Geçersiz model verisi!";
                    await LoadRegisterData();
                    return View();
                }

                if (string.IsNullOrWhiteSpace(ogrenci.Sifre) || ogrenci.Sifre != sifreTekrar)
                {
                    ViewBag.Error = "Şifreler eşleşmiyor!";
                    await LoadRegisterData();
                    return View(ogrenci);
                }

                if (!ValidateOgrenciRequiredFields(ogrenci))
                {
                    ViewBag.Error = "Lütfen tüm zorunlu alanları doldurunuz!";
                    await LoadRegisterData();
                    return View(ogrenci);
                }

                if (await _context.Ogrenciler.AnyAsync(o => o.OgrenciNo == ogrenci.OgrenciNo))
                {
                    ViewBag.Error = "Bu öğrenci numarası zaten kayıtlı!";
                    await LoadRegisterData();
                    return View(ogrenci);
                }

                if (await _context.Ogrenciler.AnyAsync(o => o.Email == ogrenci.Email))
                {
                    ViewBag.Error = "Bu e-posta adresi zaten kayıtlı!";
                    await LoadRegisterData();
                    return View(ogrenci);
                }

                ogrenci.Sifre = HashPassword(ogrenci.Sifre);
                ogrenci.KayitTarihi = DateTime.Now;
                ogrenci.Aktif = true;

                // UserProfile oluştur
                ogrenci.UserProfile = new UserProfile
                {
                    ProfilTamamlandi = false,
                    GuncellenmeTarihi = DateTime.Now
                };

                _context.Ogrenciler.Add(ogrenci);
                await _context.SaveChangesAsync();

                ViewBag.Success = "Kayıt başarılı! Giriş yapabilirsiniz.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kayıt sırasında hata oluştu.");
                ViewBag.Error = "Kayıt sırasında bir hata oluştu.";
                await LoadRegisterData();
                return View(ogrenci);
            }
        }

        // GET: Account/YetkiliRegister
        public async Task<IActionResult> YetkiliRegister()
        {
            await LoadYetkiliRegisterData();
            return View();
        }

        // POST: Account/YetkiliRegister
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliRegister(Yetkili yetkili, string sifreTekrar)
        {
            try
            {
                await LoadYetkiliRegisterData();

                if (yetkili == null)
                {
                    ViewBag.Error = "Form verisi alınamadı! Lütfen formu yeniden doldurunuz.";
                    return View();
                }

                // Önce onay aşaması bilgilerini al
                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == yetkili.OnayAsamasi && oa.Aktif);

                if (onayAsama == null)
                {
                    ViewBag.Error = "Seçilen onay aşaması geçerli değil!";
                    return View(yetkili);
                }

                // Ortak birim kontrolü - Eğer ortak ise fakülte ve bölüm bilgilerini temizle
                if (onayAsama.Ortak)
                {
                    yetkili.FakulteId = 1; // Varsayılan fakülte ID'si (genellikle "Üniversite Geneli" gibi)
                    yetkili.BolumId = null;
                }

                var eksikAlanlar = ValidateYetkiliFields(yetkili, onayAsama);
                if (eksikAlanlar.Any())
                {
                    ViewBag.Error = $"Lütfen aşağıdaki alanları doldurunuz: {string.Join(", ", eksikAlanlar)}";
                    return View(yetkili);
                }

                if (string.IsNullOrWhiteSpace(yetkili.Sifre) || yetkili.Sifre != sifreTekrar)
                {
                    ViewBag.Error = "Girilen şifreler eşleşmiyor! Lütfen tekrar kontrol ediniz.";
                    return View(yetkili);
                }

                if (!IsEmail(yetkili.Email))
                {
                    ViewBag.Error = "Lütfen geçerli bir e-posta adresi giriniz!";
                    return View(yetkili);
                }

                if (await _context.Yetkililer.AnyAsync(y => y.Email == yetkili.Email))
                {
                    ViewBag.Error = "Bu e-posta adresi zaten bir yetkili tarafından kullanılıyor!";
                    return View(yetkili);
                }

                var yetkiliSayisiKontrol = await CheckYetkiliSayisiSiniri(yetkili.OnayAsamasi, yetkili.FakulteId, yetkili.BolumId);
                if (!yetkiliSayisiKontrol.IsValid)
                {
                    ViewBag.Error = yetkiliSayisiKontrol.ErrorMessage;
                    return View(yetkili);
                }

                // Ortak birim değilse fakülte/bölüm validasyonu yap
                if (!onayAsama.Ortak && !await ValidateFakulteAndBolum(yetkili, onayAsama))
                {
                    ViewBag.Error = "Seçilen fakülte veya bölüm sistemde bulunamadı!";
                    return View(yetkili);
                }

                yetkili.Sifre = HashPassword(yetkili.Sifre);
                yetkili.OnayBekliyor = true;
                yetkili.Aktif = false;
                yetkili.KayitTarihi = DateTime.Now;
                yetkili.Rol = ApplicationConstants.YetkiliRole;

                // UserProfile oluştur
                yetkili.UserProfile = new UserProfile
                {
                    ProfilTamamlandi = false,
                    GuncellenmeTarihi = DateTime.Now
                };

                _context.Yetkililer.Add(yetkili);
                await _context.SaveChangesAsync();

                ViewBag.Success = "Yetkili başvurunuz başarıyla alınmıştır. SuperAdmin onayından sonra giriş yapabileceksiniz.";
                ModelState.Clear();
                return View(model: new Yetkili());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili kayıt sırasında hata oluştu.");
                ViewBag.Error = "Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyiniz.";
                await LoadYetkiliRegisterData();
                return View(yetkili);
            }
        }

        // Güncellenmiş validasyon metodu
        private List<string> ValidateYetkiliFields(Yetkili yetkili, OnayAsama? onayAsama = null)
        {
            var eksikAlanlar = new List<string>();
            if (string.IsNullOrWhiteSpace(yetkili.AdSoyad)) eksikAlanlar.Add("Ad Soyad");
            if (string.IsNullOrWhiteSpace(yetkili.Email)) eksikAlanlar.Add("E-posta");
            if (string.IsNullOrWhiteSpace(yetkili.Pozisyon)) eksikAlanlar.Add("Pozisyon");
            if (string.IsNullOrWhiteSpace(yetkili.OnayAsamasi)) eksikAlanlar.Add("Onay Aşaması");
            if (string.IsNullOrWhiteSpace(yetkili.Sifre)) eksikAlanlar.Add("Şifre");

            // Ortak birim değilse fakülte kontrolü yap
            if (onayAsama != null && !onayAsama.Ortak)
            {
                if (yetkili.FakulteId <= 0) eksikAlanlar.Add("Fakülte");

                // Bölüm bazlı ise bölüm kontrolü yap
                if (onayAsama.BolumBazli && (!yetkili.BolumId.HasValue || yetkili.BolumId <= 0))
                {
                    eksikAlanlar.Add("Bölüm");
                }
            }   

            return eksikAlanlar;
        }

        // Güncellenmiş fakülte/bölüm validasyonu
        private async Task<bool> ValidateFakulteAndBolum(Yetkili yetkili, OnayAsama onayAsama)
        {
            // Ortak birim ise validasyon yapma
            if (onayAsama.Ortak)
                return true;

            // Fakülte kontrolü
            if (!await _context.Fakulteler.AnyAsync(f => f.Id == yetkili.FakulteId && f.Aktif))
                return false;

            // Bölüm bazlı ise bölüm kontrolü
            if (onayAsama.BolumBazli)
            {
                if (!yetkili.BolumId.HasValue || yetkili.BolumId <= 0)
                    return false;

                if (!await _context.Bolumler.AnyAsync(b => b.Id == yetkili.BolumId && b.Aktif && b.FakulteId == yetkili.FakulteId))
                    return false;
            }

            return true;
        }

        // AJAX: Fakulteye göre bölümleri getir
        [HttpGet]
        public async Task<JsonResult> GetBolumler(int fakulteId)
        {
            try
            {
                if (fakulteId <= 0)
                    return Json(new { success = false, message = "Geçersiz fakülte ID." });

                var bolumler = await _context.Bolumler
                    .Where(b => b.FakulteId == fakulteId && b.Aktif)
                    .OrderBy(b => b.BolumAdi)
                    .Select(b => new { id = b.Id, bolumAdi = b.BolumAdi })
                    .ToListAsync();

                return Json(bolumler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bölüm yükleme hatası.");
                return Json(new { success = false, message = "Bölümler yüklenirken hata oluştu." });
            }
        }

        // GET: Account/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return RedirectToAction("Index", "PasswordReset");
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "E-posta adresi boş bırakılamaz.";
                return View();
            }

            try
            {
                var ogrenci = await _context.Ogrenciler.FirstOrDefaultAsync(o => o.Email == email);
                var yetkili = await _context.Yetkililer.FirstOrDefaultAsync(y => y.Email == email);

                if (ogrenci == null && yetkili == null)
                {
                    ViewBag.Success = "Eğer bu e-posta adresine kayıtlı bir hesap varsa, şifre sıfırlama bağlantısı gönderilmiştir.";
                    return View();
                }

                var resetToken = new PasswordResetToken
                {
                    Email = email,
                    Token = GenerateResetToken(),
                    UserType = ogrenci != null ? ApplicationConstants.OgrenciRole : ApplicationConstants.YetkiliRole,
                    ExpiresAt = DateTime.Now.AddHours(1)
                };

                var oldTokens = await _context.PasswordResetTokens
                    .Where(t => t.Email == email && !t.IsUsed)
                    .ToListAsync();

                foreach (var oldToken in oldTokens)
                {
                    oldToken.IsUsed = true;
                }

                _context.PasswordResetTokens.Add(resetToken);
                await _context.SaveChangesAsync();

                SendPasswordResetEmail(email, resetToken.Token);

                ViewBag.Success = "Şifre sıfırlama bağlantısı e-posta adresinize gönderilmiştir.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu.");
                ViewBag.Error = "İşlem sırasında bir hata oluştu.";
                return View();
            }
        }

        // GET: Account/ResetPassword
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Geçersiz token.";
                return RedirectToAction("Login");
            }

            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.Now);

            if (resetToken == null)
            {
                ViewBag.Error = "Token geçersiz veya süresi dolmuş.";
                return RedirectToAction("Login");
            }

            ViewBag.Token = token;
            ViewBag.Email = resetToken.Email;
            return View();
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "Gerekli alanlar boş bırakılamaz.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Şifreler eşleşmiyor.";
                ViewBag.Token = token;
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Şifre en az 6 karakter olmalıdır.";
                ViewBag.Token = token;
                return View();
            }

            try
            {
                var resetToken = await _context.PasswordResetTokens
                    .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.Now);

                if (resetToken == null)
                {
                    ViewBag.Error = "Token geçersiz veya süresi dolmuş.";
                    return View();
                }

                var hashedPassword = HashPassword(newPassword);

                if (resetToken.UserType == ApplicationConstants.OgrenciRole)
                {
                    var ogrenci = await _context.Ogrenciler.FirstOrDefaultAsync(o => o.Email == resetToken.Email);
                    if (ogrenci != null)
                    {
                        ogrenci.Sifre = hashedPassword;
                    }
                    else
                    {
                        ViewBag.Error = "Kullanıcı bulunamadı.";
                        return View();
                    }
                }
                else if (resetToken.UserType == ApplicationConstants.YetkiliRole)
                {
                    var yetkili = await _context.Yetkililer.FirstOrDefaultAsync(y => y.Email == resetToken.Email);
                    if (yetkili != null)
                    {
                        yetkili.Sifre = hashedPassword;
                    }
                    else
                    {
                        ViewBag.Error = "Kullanıcı bulunamadı.";
                        return View();
                    }
                }

                resetToken.IsUsed = true;
                await _context.SaveChangesAsync();

                ViewBag.Success = "Şifreniz başarıyla değiştirildi. Giriş yapabilirsiniz.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu.");
                ViewBag.Error = "İşlem sırasında bir hata oluştu.";
                ViewBag.Token = token;
                return View();
            }
        }

        #region Private Helper Methods

        private async Task<IActionResult> HandleYetkiliLogin(string email, string hashedPassword)
        {
            var yetkili = await _context.Yetkililer
                .Include(y => y.Fakulte)
                .Include(y => y.Bolum)
                .FirstOrDefaultAsync(y => y.Email == email && y.Sifre == hashedPassword && y.Aktif && !y.OnayBekliyor);

            if (yetkili == null)
            {
                ViewBag.Error = "E-posta veya şifre yanlış, ya da hesabınız onay bekliyor.";
                return View();
            }

            // Session bilgilerini set et
            HttpContext.Session.SetString("UserId", yetkili.Id.ToString());
            HttpContext.Session.SetString("UserType", yetkili.Rol);
            HttpContext.Session.SetString("UserName", yetkili.AdSoyad);
            HttpContext.Session.SetString("FakulteId", yetkili.FakulteId.ToString());
            HttpContext.Session.SetInt32("BolumId", yetkili.BolumId ?? 0);
            HttpContext.Session.SetString("FakulteAdi", yetkili.Fakulte?.FakulteAdi ?? "Bilinmeyen Fakülte");
            HttpContext.Session.SetString("OnayAsamasi", yetkili.OnayAsamasi);

            // Son aktivite tarihini güncelle
            yetkili.SonAktiviteTarihi = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Yetkili");
        }

        private async Task<IActionResult> HandleOgrenciLogin(string ogrenciNo, string hashedPassword)
        {
            var ogrenci = await _context.Ogrenciler
                .Include(o => o.Fakulte)
                .Include(o => o.Bolum)
                .FirstOrDefaultAsync(o => o.OgrenciNo == ogrenciNo && o.Sifre == hashedPassword && o.Aktif);

            if (ogrenci == null)
            {
                ViewBag.Error = "Öğrenci numarası veya şifre yanlış.";
                return View();
            }

            HttpContext.Session.SetString("UserId", ogrenci.Id.ToString());
            HttpContext.Session.SetString("UserType", ApplicationConstants.OgrenciRole);
            HttpContext.Session.SetString("UserName", ogrenci.TamAd);
            HttpContext.Session.SetString("FakulteId", ogrenci.FakulteId.ToString());
            HttpContext.Session.SetString("FakulteAdi", ogrenci.Fakulte?.FakulteAdi ?? "Bilinmeyen Fakülte");
            HttpContext.Session.SetString("BolumId", ogrenci.BolumId.ToString());
            HttpContext.Session.SetString("OgrenciNo", ogrenci.OgrenciNo);

            return RedirectToAction("Index", "Ogrenci");
        }

        private async Task<(bool IsValid, string ErrorMessage)> CheckYetkiliSayisiSiniri(string onayAsamasi, int fakulteId, int? bolumId)
        {
            try
            {
                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi);

                if (onayAsama == null)
                {
                    return (false, "Seçilen onay aşaması tanımlı değil!");
                }

                var mevcutYetkiliSayisi = await GetMevcutYetkiliSayisi(onayAsamasi, onayAsama, fakulteId, bolumId);

                if (mevcutYetkiliSayisi >= onayAsama.MaxYetkiliSayisi)
                {
                    var limitMesaji = GetYetkiliLimitMesaji(onayAsama, fakulteId, bolumId);
                    return (false, $"Bu pozisyon için maksimum yetkili sayısına ulaşılmış! {limitMesaji}");
                }

                if (onayAsama.BolumBazli && (!bolumId.HasValue || bolumId <= 0))
                {
                    return (false, "Bu pozisyon için bölüm seçimi zorunludur!");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili sayısı kontrolü sırasında hata.");
                return (false, $"Kontrol sırasında hata oluştu.");
            }
        }

        private async Task<int> GetMevcutYetkiliSayisi(string onayAsamasi, OnayAsama onayAsama, int fakulteId, int? bolumId)
        {
            var query = _context.Yetkililer
                .Where(y => y.OnayAsamasi == onayAsamasi && (y.Aktif || y.OnayBekliyor));

            if (onayAsama.Ortak)
            {
                return await query.CountAsync();
            }
            else if (onayAsama.BolumBazli && bolumId.HasValue)
            {
                return await query.CountAsync(y => y.FakulteId == fakulteId && y.BolumId == bolumId);
            }
            else if (onayAsama.FakulteBazli)
            {
                return await query.CountAsync(y => y.FakulteId == fakulteId);
            }
            else
            {
                return await query.CountAsync();
            }
        }

        private string GetYetkiliLimitMesaji(OnayAsama onayAsama, int fakulteId, int? bolumId)
        {
            if (onayAsama.Ortak)
            {
                return $"Üniversite genelinde maksimum {onayAsama.MaxYetkiliSayisi} yetkili olabilir.";
            }
            else if (onayAsama.BolumBazli)
            {
                return $"Bu bölümde maksimum {onayAsama.MaxYetkiliSayisi} yetkili olabilir.";
            }
            else if (onayAsama.FakulteBazli)
            {
                return $"Bu fakültede maksimum {onayAsama.MaxYetkiliSayisi} yetkili olabilir.";
            }
            else
            {
                return $"Maksimum {onayAsama.MaxYetkiliSayisi} yetkili olabilir.";
            }
        }

        private bool IsEmail(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && input.Contains("@") && input.Contains(".");
        }

        private bool ValidateOgrenciRequiredFields(Ogrenci ogrenci)
        {
            return !string.IsNullOrWhiteSpace(ogrenci.Ad) &&
                   !string.IsNullOrWhiteSpace(ogrenci.Soyad) &&
                   !string.IsNullOrWhiteSpace(ogrenci.OgrenciNo) &&
                   !string.IsNullOrWhiteSpace(ogrenci.Email) &&
                   ogrenci.FakulteId > 0 &&
                   ogrenci.BolumId > 0;
        }

        private List<string> ValidateYetkiliFields(Yetkili yetkili)
        {
            var eksikAlanlar = new List<string>();
            if (string.IsNullOrWhiteSpace(yetkili.AdSoyad)) eksikAlanlar.Add("Ad Soyad");
            if (string.IsNullOrWhiteSpace(yetkili.Email)) eksikAlanlar.Add("E-posta");
            if (string.IsNullOrWhiteSpace(yetkili.Pozisyon)) eksikAlanlar.Add("Pozisyon");
            if (string.IsNullOrWhiteSpace(yetkili.OnayAsamasi)) eksikAlanlar.Add("Onay Aşaması");
            if (yetkili.FakulteId <= 0) eksikAlanlar.Add("Fakülte");
            if (yetkili.BolumId.HasValue && yetkili.BolumId <= 0) eksikAlanlar.Add("Bölüm");
            if (string.IsNullOrWhiteSpace(yetkili.Sifre)) eksikAlanlar.Add("Şifre");
            return eksikAlanlar;
        }

        private async Task<bool> ValidateFakulteAndBolum(Yetkili yetkili)
        {
            if (!await _context.Fakulteler.AnyAsync(f => f.Id == yetkili.FakulteId && f.Aktif))
                return false;

            var onayAsama = await _context.OnayAsamalari.FirstOrDefaultAsync(oa => oa.AsamaAdi == yetkili.OnayAsamasi);
            if (onayAsama?.BolumBazli == true && (!yetkili.BolumId.HasValue || yetkili.BolumId <= 0))
                return false;

            if (yetkili.BolumId.HasValue && !await _context.Bolumler.AnyAsync(b => b.Id == yetkili.BolumId && b.Aktif))
                return false;

            return true;
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return string.Empty;

            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        
        [HttpGet]
        public async Task<JsonResult> GetOnayAsamasiDetay(string onayAsamasi)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(onayAsamasi))
                    return Json(new { success = false, message = "Geçersiz onay aşaması." });

                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi && oa.Aktif);

                if (onayAsama == null)
                    return Json(new { success = false, message = "Onay aşaması bulunamadı." });

                return Json(new
                {
                    success = true,
                    onayAsama = new
                    {
                        ortak = onayAsama.Ortak,
                        fakulteBazli = onayAsama.FakulteBazli,
                        bolumBazli = onayAsama.BolumBazli,
                        maxYetkiliSayisi = onayAsama.MaxYetkiliSayisi
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onay aşaması detayı yüklenirken hata oluştu.");
                return Json(new { success = false, message = "Onay aşaması detayı yüklenirken hata oluştu." });
            }
        }

        private string GenerateResetToken()
        {
            return Guid.NewGuid().ToString("N")[..32];
        }

        private void SendPasswordResetEmail(string email, string token)
        {
            try
            {
                // Email gönderme implementasyonu
                // Bu kısım gerçek SMTP ayarlarına göre yapılandırılmalı
                _logger.LogInformation($"Şifre sıfırlama e-postası gönderildi: {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama e-postası gönderirken hata oluştu.");
            }
        }

        private async Task LoadRegisterData()
        {
            try
            {
                var fakulteler = await _context.Fakulteler
                    .Where(f => f.Aktif)
                    .OrderBy(f => f.FakulteAdi)
                    .ToListAsync();

                ViewBag.Fakulteler = fakulteler;

                if (fakulteler == null || !fakulteler.Any())
                {
                    ViewBag.Warning = "Sistemde aktif fakülte bulunmamaktadır. Lütfen yönetici ile iletişime geçiniz.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fakulteler yüklenirken hata oluştu.");
                ViewBag.Error = "Fakulteler yüklenirken hata oluştu.";
                ViewBag.Fakulteler = new List<Fakulte>();
            }
        }

        private async Task LoadYetkiliRegisterData()
        {
            try
            {
                var fakulteler = await _context.Fakulteler
                    .Where(f => f.Aktif)
                    .OrderBy(f => f.FakulteAdi)
                    .ToListAsync();

                ViewBag.Fakulteler = fakulteler ?? new List<Fakulte>();

                if (fakulteler == null || !fakulteler.Any())
                {
                    ViewBag.Warning = "Sistemde aktif fakülte bulunmamaktadır. Lütfen yönetici ile iletişime geçiniz.";
                }

                var onayAsamalari = await GetOnayAsamalariList();
                ViewBag.OnayAsamalari = new SelectList(onayAsamalari, "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili kayıt verileri yüklenirken hata oluştu.");
                ViewBag.Error = "Sayfa yüklenirken hata oluştu.";
                ViewBag.Fakulteler = new List<Fakulte>();
                ViewBag.OnayAsamalari = new SelectList(new List<object>(), "Value", "Text");
            }
        }

        public async Task<List<SelectListItem>> GetOnayAsamalariList()
        {
            return await _context.OnayAsamalari
                .Where(oa => oa.Aktif)
                .OrderBy(oa => oa.AsamaAdi)
                .Select(oa => new SelectListItem
                {
                    Value = oa.AsamaAdi,
                    Text = oa.AsamaAdi
                })
                .ToListAsync();
        }

        #endregion
    }
}