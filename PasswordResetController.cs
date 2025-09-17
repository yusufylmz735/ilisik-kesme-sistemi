using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using HayataAtilmaFormu.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace HayataAtilmaFormu.Controllers
{
    public class PasswordResetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<PasswordResetController> _logger;

        public PasswordResetController(ApplicationDbContext context, ISmsService smsService, ILogger<PasswordResetController> logger)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
        }

        // GET: PasswordReset/Index
        public IActionResult Index()
        {
            return View();
        }

        // POST: PasswordReset/SendResetCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendResetCode(string identifier, string userType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(userType))
                {
                    return Json(new { success = false, message = "Lütfen tüm alanları doldurun." });
                }

                string phoneNumber = "";
                string email = "";
                string fullName = "";
                object user = null!;

                if (userType == "Ogrenci")
                {
                    var ogrenci = await _context.Ogrenciler
                        .FirstOrDefaultAsync(o => o.OgrenciNo == identifier || o.Email == identifier);

                    if (ogrenci == null)
                    {
                        return Json(new { success = false, message = "Öğrenci bulunamadı!" });
                    }

                    if (string.IsNullOrEmpty(ogrenci.Telefon))
                    {
                        return Json(new { success = false, message = "Telefon numarası kayıtlı değil. Lütfen öğrenci işleri ile iletişime geçin." });
                    }

                    phoneNumber = ogrenci.Telefon;
                    email = ogrenci.Email;
                    fullName = ogrenci.TamAd;
                    user = ogrenci;
                }
                else if (userType == "Yetkili")
                {
                    var yetkili = await _context.Yetkililer
                        .FirstOrDefaultAsync(y => y.Email == identifier);

                    if (yetkili == null)
                    {
                        return Json(new { success = false, message = "Yetkili bulunamadı!" });
                    }

                    if (string.IsNullOrEmpty(yetkili.Telefon))
                    {
                        return Json(new { success = false, message = "Telefon numarası kayıtlı değil. Lütfen admin ile iletişime geçin." });
                    }

                    phoneNumber = yetkili.Telefon;
                    email = yetkili.Email;
                    fullName = yetkili.AdSoyad;
                    user = yetkili;
                }
                else
                {
                    return Json(new { success = false, message = "Geçersiz kullanıcı türü!" });
                }

                // Yeni rastgele şifre oluştur
                var newPassword = await _smsService.GenerateRandomPassword();
                _logger.LogInformation("Yeni şifre oluşturuldu: {Email}", email);

                // Şifreyi hashle
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                // Veritabanında şifreyi güncelle
                if (userType == "Ogrenci" && user is Ogrenci ogrenciToUpdate)
                {
                    ogrenciToUpdate.Sifre = hashedPassword;
                    ogrenciToUpdate.GuncellenmeTarihi = DateTime.Now;
                    _context.Ogrenciler.Update(ogrenciToUpdate);
                }
                else if (userType == "Yetkili" && user is Yetkili yetkiliToUpdate)
                {
                    yetkiliToUpdate.Sifre = hashedPassword;
                    yetkiliToUpdate.GuncellenmeTarihi = DateTime.Now;
                    _context.Yetkililer.Update(yetkiliToUpdate);
                }

                // Token oluştur (log tutmak için)
                var token = new PasswordResetToken
                {
                    Email = email,
                    Token = Guid.NewGuid().ToString(),
                    UserType = userType,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddHours(1),
                    IsUsed = true // SMS ile direkt şifre gönderildiği için kullanılmış kabul et
                };

                _context.PasswordResetTokens.Add(token);

                // Değişiklikleri kaydet
                await _context.SaveChangesAsync();
                _logger.LogInformation("Şifre veritabanında güncellendi: {Email}", email);

                // SMS gönder
                var smsResult = await _smsService.SendPasswordResetSmsAsync(phoneNumber, newPassword, userType);

                if (smsResult)
                {
                    _logger.LogInformation("Şifre sıfırlama SMS'i başarıyla gönderildi: {Email} - {PhoneNumber}", email, phoneNumber);

                    return Json(new
                    {
                        success = true,
                        message = $"Yeni şifreniz {FormatPhoneNumber(phoneNumber)} numaralı telefona SMS olarak gönderildi. Lütfen SMS'inizi kontrol edin."
                    });
                }
                else
                {
                    _logger.LogError("SMS gönderilemedi: {Email} - {PhoneNumber}", email, phoneNumber);

                    // SMS gönderilemezse şifreyi eski haline döndür
                    // Ancak güvenlik açısından bu adımı atlayıp manuel müdahale bekleyebiliriz

                    return Json(new
                    {
                        success = false,
                        message = "SMS gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin veya sistem yöneticisi ile iletişime geçin."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu: {Identifier} - {UserType}", identifier, userType);
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu. Lütfen tekrar deneyin." });
            }
        }

        // POST: PasswordReset/ResendCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(string email, string userType)
        {
            try
            {
                // Çok sık tekrar gönderim engelleme (5 dakika)
                var recentToken = await _context.PasswordResetTokens
                    .Where(t => t.Email == email && t.UserType == userType)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (recentToken != null && recentToken.CreatedAt.AddMinutes(5) > DateTime.Now)
                {
                    var kalanSure = (int)recentToken.CreatedAt.AddMinutes(5).Subtract(DateTime.Now).TotalMinutes + 1;
                    return Json(new
                    {
                        success = false,
                        message = $"Çok sık kod talep ediyorsunuz. {kalanSure} dakika bekleyip tekrar deneyin."
                    });
                }

                // Yeniden kod gönder
                return await SendResetCode(email, userType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kod tekrar gönderimi sırasında hata oluştu: {Email}", email);
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu." });
            }
        }

        // GET: PasswordReset/Success
        public IActionResult Success()
        {
            return View();
        }

        // GET: PasswordReset/ResetHistory - Admin için geçmiş kayıtları görüntüleme
        public async Task<IActionResult> ResetHistory(int page = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.PasswordResetTokens
                    .OrderByDescending(t => t.CreatedAt)
                    .AsQueryable();

                var totalRecords = await query.CountAsync();
                var tokens = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
                ViewBag.TotalRecords = totalRecords;

                return View(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama geçmişi yüklenirken hata oluştu.");
                ViewBag.Error = "Veriler yüklenirken hata oluştu.";
                return View(new List<PasswordResetToken>());
            }
        }

        // POST: PasswordReset/CleanupOldTokens - Eski tokenları temizleme (Admin için)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanupOldTokens(int olderThanDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                var oldTokens = await _context.PasswordResetTokens
                    .Where(t => t.CreatedAt < cutoffDate)
                    .ToListAsync();

                if (oldTokens.Any())
                {
                    _context.PasswordResetTokens.RemoveRange(oldTokens);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Eski şifre sıfırlama tokenleri temizlendi: {Count}", oldTokens.Count);
                    return Json(new { success = true, message = $"{oldTokens.Count} eski kayıt temizlendi." });
                }
                else
                {
                    return Json(new { success = true, message = "Temizlenecek eski kayıt bulunamadı." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token temizleme işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "Temizleme işlemi sırasında hata oluştu." });
            }
        }

        // Helper method - telefon numarasını güvenli şekilde göstermek için
        private string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return phoneNumber;

            // Telefon numarasının son 4 hanesini göster, diğerlerini gizle
            if (phoneNumber.Length > 4)
            {
                var visiblePart = phoneNumber.Substring(phoneNumber.Length - 4);
                var hiddenPart = new string('*', phoneNumber.Length - 4);
                return hiddenPart + visiblePart;
            }

            return phoneNumber;
        }

        // GET: Test SMS functionality
        [HttpGet]
        public async Task<IActionResult> TestSms(string phoneNumber = "+905555555555", string message = "Test mesajı")
        {
            try
            {
                var result = await _smsService.SendSmsAsync(phoneNumber, message);
                return Json(new { success = result, message = result ? "SMS başarıyla gönderildi" : "SMS gönderilemedi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS test hatası");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}