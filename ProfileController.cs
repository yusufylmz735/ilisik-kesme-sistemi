using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using HayataAtilmaFormu.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace HayataAtilmaFormu.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<ProfileController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // Giriş kontrolü
        private bool CheckUserLogin()
        {
            var userType = HttpContext.Session.GetString("UserType");
            var userId = HttpContext.Session.GetString("UserId");

            return !string.IsNullOrEmpty(userType) && !string.IsNullOrEmpty(userId) &&
                   (userType == ApplicationConstants.OgrenciRole ||
                    userType == ApplicationConstants.YetkiliRole ||
                    userType == ApplicationConstants.SuperAdminRole);
        }

        // GET: Profile/Index
        public async Task<IActionResult> Index()
        {
            if (!CheckUserLogin())
                return RedirectToAction("Login", "Account");

            var userId = HttpContext.Session.GetString("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (!int.TryParse(userId, out var parsedUserId))
            {
                _logger.LogWarning("Geçersiz UserId formatı.");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var profileViewModel = await GetProfileViewModelAsync(parsedUserId, userType);

                if (profileViewModel == null)
                {
                    ViewBag.Error = "Profil bilgileri yüklenirken hata oluştu.";
                    return View(new ProfileViewModel());
                }

                return View(profileViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil yüklenirken hata oluştu.");
                ViewBag.Error = "Profil yüklenirken hata oluştu.";
                return View(new ProfileViewModel());
            }
        }

        // GET: Profile/Edit
        public async Task<IActionResult> Edit()
        {
            if (!CheckUserLogin())
                return RedirectToAction("Login", "Account");

            var userId = HttpContext.Session.GetString("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var profileViewModel = await GetProfileViewModelAsync(parsedUserId, userType);

                if (profileViewModel == null)
                {
                    ViewBag.Error = "Profil bilgileri yüklenirken hata oluştu.";
                    return RedirectToAction("Index");
                }

                await LoadEditViewData();
                return View(profileViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil düzenleme sayfası yüklenirken hata oluştu.");
                ViewBag.Error = "Profil düzenleme sayfası yüklenirken hata oluştu.";
                return RedirectToAction("Index");
            }
        }

        // POST: Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileViewModel model, IFormFile? profilePhoto)
        {
            if (!CheckUserLogin())
                return RedirectToAction("Login", "Account");

            var userId = HttpContext.Session.GetString("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (profilePhoto != null && profilePhoto.Length > 0)
                {
                    var photoPath = await UploadProfilePhotoAsync(profilePhoto, parsedUserId);
                    if (photoPath != null)
                    {
                        model.ProfilFotoYolu = photoPath;
                    }
                }

                var success = false;

                if (userType == ApplicationConstants.OgrenciRole)
                {
                    success = await UpdateOgrenciProfileAsync(parsedUserId, model);
                }
                else if (userType == ApplicationConstants.YetkiliRole || userType == ApplicationConstants.SuperAdminRole)
                {
                    success = await UpdateYetkiliProfileAsync(parsedUserId, model);
                }

                if (success)
                {
                    ViewBag.Success = "Profil başarıyla güncellendi.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.Error = "Profil güncellenirken hata oluştu.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil güncellenirken hata oluştu.");
                ViewBag.Error = "Profil güncellenirken hata oluştu.";
            }

            await LoadEditViewData();
            return View(model);
        }

        // GET: Profile/ChangePassword
        public IActionResult ChangePassword()
        {
            if (!CheckUserLogin())
                return RedirectToAction("Login", "Account");

            return View();
        }

        // POST: Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!CheckUserLogin())
                return RedirectToAction("Login", "Account");

            var userId = HttpContext.Session.GetString("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ViewBag.Error = "Şifreler eşleşmiyor.";
                return View(model);
            }

            try
            {
                var hashedNewPassword = HashPassword(model.NewPassword);
                var hashedCurrentPassword = HashPassword(model.CurrentPassword);

                if (userType == ApplicationConstants.OgrenciRole)
                {
                    var ogrenci = await _context.Ogrenciler.FindAsync(parsedUserId);
                    if (ogrenci != null && ogrenci.Sifre == hashedCurrentPassword)
                    {
                        ogrenci.Sifre = hashedNewPassword;
                        await _context.SaveChangesAsync();
                        ViewBag.Success = "Şifre başarıyla değiştirildi.";
                        return View();
                    }
                }
                else if (userType == ApplicationConstants.YetkiliRole || userType == ApplicationConstants.SuperAdminRole)
                {
                    var yetkili = await _context.Yetkililer.FindAsync(parsedUserId);
                    if (yetkili != null && yetkili.Sifre == hashedCurrentPassword)
                    {
                        yetkili.Sifre = hashedNewPassword;
                        await _context.SaveChangesAsync();
                        ViewBag.Success = "Şifre başarıyla değiştirildi.";
                        return View();
                    }
                }

                ViewBag.Error = "Mevcut şifre yanlış.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre değiştirme sırasında hata oluştu.");
                ViewBag.Error = "Şifre değiştirme sırasında hata oluştu.";
                return View(model);
            }
        }

        // Private Helper Methods
        private async Task<ProfileViewModel?> GetProfileViewModelAsync(int userId, string? userType)
        {
            try
            {
                if (userType == ApplicationConstants.OgrenciRole)
                {
                    var ogrenci = await _context.Ogrenciler
                        .Include(o => o.Fakulte)
                        .Include(o => o.Bolum)
                        .Include(o => o.UserProfile)
                        .FirstOrDefaultAsync(o => o.Id == userId);

                    if (ogrenci == null) return null;

                    return new ProfileViewModel
                    {
                        Id = ogrenci.Id,
                        UserType = ApplicationConstants.OgrenciRole,
                        AdSoyad = ogrenci.TamAd,
                        Email = ogrenci.Email,
                        Telefon = ogrenci.Telefon,
                        OgrenciNo = ogrenci.OgrenciNo,
                        FakulteId = ogrenci.FakulteId,
                        FakulteAdi = ogrenci.Fakulte?.FakulteAdi ?? "",
                        BolumId = ogrenci.BolumId,
                        BolumAdi = ogrenci.Bolum?.BolumAdi ?? "",
                        EgitimTuru = ogrenci.Bolum?.EgitimTuru ?? "",
                        KayitTarihi = ogrenci.KayitTarihi,
                        ProfilFotoYolu = ogrenci.UserProfile?.ProfilFotoYolu,
                        Hakkinda = ogrenci.UserProfile?.Hakkinda,
                        Website = ogrenci.UserProfile?.Website,
                        LinkedIn = ogrenci.UserProfile?.LinkedIn,
                        Twitter = ogrenci.UserProfile?.Twitter,
                        Adres = ogrenci.UserProfile?.Adres,
                        DogumTarihi = !string.IsNullOrEmpty(ogrenci.UserProfile?.DogumTarihi) ?
                            DateTime.TryParse(ogrenci.UserProfile.DogumTarihi, out var dt) ? dt : null : null,
                        Cinsiyet = ogrenci.UserProfile?.Cinsiyet,
                        Universite = ogrenci.UserProfile?.Universite,
                        Mezuniyet = ogrenci.UserProfile?.Mezuniyet,
                        ProfilTamamlandi = ogrenci.UserProfile?.ProfilTamamlandi ?? false,
                        GuncellenmeTarihi = ogrenci.UserProfile?.GuncellenmeTarihi ?? DateTime.Now
                    };
                }
                else if (userType == ApplicationConstants.YetkiliRole || userType == ApplicationConstants.SuperAdminRole)
                {
                    var yetkili = await _context.Yetkililer
                        .Include(y => y.Fakulte)
                        .Include(y => y.Bolum)
                        .Include(y => y.UserProfile)
                        .FirstOrDefaultAsync(y => y.Id == userId);

                    if (yetkili == null) return null;

                    return new ProfileViewModel
                    {
                        Id = yetkili.Id,
                        UserType = userType,
                        AdSoyad = yetkili.AdSoyad,
                        Email = yetkili.Email,
                        Telefon = yetkili.Telefon,
                        FakulteId = yetkili.FakulteId,
                        FakulteAdi = yetkili.Fakulte?.FakulteAdi ?? "",
                        BolumId = yetkili.BolumId,
                        BolumAdi = yetkili.Bolum?.BolumAdi ?? "",
                        Pozisyon = yetkili.Pozisyon,
                        OnayAsamasi = yetkili.OnayAsamasi,
                        Rol = yetkili.Rol,
                        Aciklama = yetkili.Aciklama,
                        KayitTarihi = yetkili.KayitTarihi,
                        ProfilFotoYolu = yetkili.UserProfile?.ProfilFotoYolu,
                        Hakkinda = yetkili.UserProfile?.Hakkinda,
                        Website = yetkili.UserProfile?.Website,
                        LinkedIn = yetkili.UserProfile?.LinkedIn,
                        Twitter = yetkili.UserProfile?.Twitter,
                        Adres = yetkili.UserProfile?.Adres,
                        DogumTarihi = !string.IsNullOrEmpty(yetkili.UserProfile?.DogumTarihi) ?
                            DateTime.TryParse(yetkili.UserProfile.DogumTarihi, out var dt) ? dt : null : null,
                        Cinsiyet = yetkili.UserProfile?.Cinsiyet,
                        Universite = yetkili.UserProfile?.Universite,
                        Mezuniyet = yetkili.UserProfile?.Mezuniyet,
                        ProfilTamamlandi = yetkili.UserProfile?.ProfilTamamlandi ?? false,
                        GuncellenmeTarihi = yetkili.UserProfile?.GuncellenmeTarihi ?? DateTime.Now
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil view model oluşturulurken hata oluştu.");
                return null;
            }
        }

        private async Task<bool> UpdateOgrenciProfileAsync(int userId, ProfileViewModel model)
        {
            try
            {
                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.UserProfile)
                    .FirstOrDefaultAsync(o => o.Id == userId);

                if (ogrenci == null) return false;

                // Öğrenci temel bilgilerini güncelle
                ogrenci.Ad = model.AdSoyad?.Split(' ')[0] ?? ogrenci.Ad;
                ogrenci.Soyad = model.AdSoyad?.Contains(' ') == true ?
                    string.Join(" ", model.AdSoyad.Split(' ').Skip(1)) : ogrenci.Soyad;
                ogrenci.Email = model.Email ?? ogrenci.Email;
                ogrenci.Telefon = model.Telefon ?? ogrenci.Telefon;

                // UserProfile güncelle veya oluştur
                if (ogrenci.UserProfile == null)
                {
                    ogrenci.UserProfile = new UserProfile { OgrenciId = userId };
                    _context.UserProfiles.Add(ogrenci.UserProfile);
                }

                UpdateUserProfile(ogrenci.UserProfile, model);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci profili güncellenirken hata oluştu.");
                return false;
            }
        }

        private async Task<bool> UpdateYetkiliProfileAsync(int userId, ProfileViewModel model)
        {
            try
            {
                var yetkili = await _context.Yetkililer
                    .Include(y => y.UserProfile)
                    .FirstOrDefaultAsync(y => y.Id == userId);

                if (yetkili == null) return false;

                // Yetkili temel bilgilerini güncelle
                yetkili.AdSoyad = model.AdSoyad ?? yetkili.AdSoyad;
                yetkili.Email = model.Email ?? yetkili.Email;
                yetkili.Telefon = model.Telefon ?? yetkili.Telefon;
                yetkili.Aciklama = model.Aciklama ?? yetkili.Aciklama;

                // UserProfile güncelle veya oluştur
                if (yetkili.UserProfile == null)
                {
                    yetkili.UserProfile = new UserProfile { YetkiliId = userId };
                    _context.UserProfiles.Add(yetkili.UserProfile);
                }

                UpdateUserProfile(yetkili.UserProfile, model);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili profili güncellenirken hata oluştu.");
                return false;
            }
        }

        private void UpdateUserProfile(UserProfile userProfile, ProfileViewModel model)
        {
            userProfile.Hakkinda = model.Hakkinda ?? userProfile.Hakkinda;
            userProfile.Website = model.Website ?? userProfile.Website;
            userProfile.LinkedIn = model.LinkedIn ?? userProfile.LinkedIn;
            userProfile.Twitter = model.Twitter ?? userProfile.Twitter;
            userProfile.Adres = model.Adres ?? userProfile.Adres;
            userProfile.DogumTarihi = model.DogumTarihi?.ToString("yyyy-MM-dd") ?? userProfile.DogumTarihi;
            userProfile.Cinsiyet = model.Cinsiyet ?? userProfile.Cinsiyet;
            userProfile.Universite = model.Universite ?? userProfile.Universite;
            userProfile.Mezuniyet = model.Mezuniyet ?? userProfile.Mezuniyet;
            userProfile.GuncellenmeTarihi = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(model.ProfilFotoYolu))
            {
                userProfile.ProfilFotoYolu = model.ProfilFotoYolu;
            }

            // Profil tamamlama durumunu kontrol et
            userProfile.ProfilTamamlandi = IsProfileComplete(model);
        }

        private bool IsProfileComplete(ProfileViewModel model)
        {
            return !string.IsNullOrWhiteSpace(model.AdSoyad) &&
                   !string.IsNullOrWhiteSpace(model.Email) &&
                   !string.IsNullOrWhiteSpace(model.Telefon) &&
                   !string.IsNullOrWhiteSpace(model.Hakkinda) &&
                   model.DogumTarihi.HasValue;
        }

        private async Task<string?> UploadProfilePhotoAsync(IFormFile profilePhoto, int userId)
        {
            try
            {
                if (profilePhoto.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    _logger.LogWarning("Profil fotoğrafı çok büyük: {Size}", profilePhoto.Length);
                    return null;
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(profilePhoto.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Desteklenmeyen dosya türü: {Extension}", fileExtension);
                    return null;
                }

                var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var fileName = $"profile_{userId}_{DateTime.Now.Ticks}{fileExtension}";
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePhoto.CopyToAsync(stream);
                }

                return $"/uploads/profiles/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Profil fotoğrafı yüklenirken hata oluştu.");
                return null;
            }
        }

        private async Task LoadEditViewData()
        {
            try
            {
                var fakulteler = await _context.Fakulteler
                    .Where(f => f.Aktif)
                    .OrderBy(f => f.FakulteAdi)
                    .ToListAsync();

                ViewBag.Fakulteler = fakulteler;

                if (!fakulteler.Any())
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
    }
}