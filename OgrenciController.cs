using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HayataAtilmaFormu.Controllers
{
    public class OgrenciController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OgrenciController> _logger;
        private readonly IWebHostEnvironment _environment;

        // Dosya upload ayarları
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
        private static readonly long MaxFileSize = 5 * 1024 * 1024; // 5MB
        private const string UploadFolder = "uploads/basvuru-belgeleri";

        public OgrenciController(ApplicationDbContext context, ILogger<OgrenciController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // Öğrenci girişi kontrolü
        private bool CheckOgrenciLogin()
        {
            var userType = HttpContext.Session.GetString("UserType");
            var userId = HttpContext.Session.GetString("UserId");

            return userType == ApplicationConstants.OgrenciRole && !string.IsNullOrEmpty(userId);
        }

        // GET: Ogrenci/Index
        public async Task<IActionResult> Index()
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                _logger.LogWarning("Geçersiz UserId formatı.");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .Include(o => o.UserProfile)
                    .FirstOrDefaultAsync(o => o.Id == userId);

                if (ogrenci == null)
                {
                    _logger.LogWarning("Öğrenci bulunamadı.");
                    return RedirectToAction("Login", "Account");
                }

                var mevcutBasvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                ViewBag.MevcutBasvuru = mevcutBasvuru;
                ViewBag.Ogrenci = ogrenci;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci ana sayfası yüklenirken hata oluştu.");
                ViewBag.Error = "Sayfa yüklenirken hata oluştu.";
                return View();
            }
        }

        // GET: Ogrenci/YeniBasvuru
        public async Task<IActionResult> YeniBasvuru()
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var mevcutBasvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari) // OnayDetaylari'nı da yükle
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                if (mevcutBasvuru != null)
                {
                    // Reddedilmiş başvuru varsa temizle
                    if (mevcutBasvuru.Durum == ApplicationConstants.Reddedildi)
                    {
                        _logger.LogInformation($"Reddedilen başvuru temizleniyor: {mevcutBasvuru.Id}");

                        // Önce OnayDetaylari'nı sil (Foreign Key constraint)
                        if (mevcutBasvuru.OnayDetaylari != null && mevcutBasvuru.OnayDetaylari.Any())
                        {
                            _context.OnayDetaylari.RemoveRange(mevcutBasvuru.OnayDetaylari);
                            _logger.LogInformation($"{mevcutBasvuru.OnayDetaylari.Count} adet OnayDetay siliniyor");
                        }

                        // Sonra Basvuru'yu sil
                        _context.Basvurular.Remove(mevcutBasvuru);

                        // Değişiklikleri veritabanına kaydet
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Reddedilen başvuru başarıyla temizlendi: {mevcutBasvuru.Id}");

                        // Temizlik sonrası yeni form göster
                        ViewBag.InfoMessage = "Reddedilmiş başvurunuz temizlendi. Yeni başvuru yapabilirsiniz.";
                    }
                    else if (mevcutBasvuru.Durum == ApplicationConstants.Beklemede)
                    {
                        ViewBag.Error = "Henüz işlemde olan başvurunuz bulunmaktadır. Lütfen mevcut başvurunuzun sonuçlanmasını bekleyiniz.";
                        return RedirectToAction("Index");
                    }
                    else if (mevcutBasvuru.Durum == ApplicationConstants.Onaylandi)
                    {
                        ViewBag.Error = "Onaylanmış başvurunuz bulunmaktadır. İlişik kesme işleminiz tamamlanmıştır.";
                        return RedirectToAction("Index");
                    }
                }

                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .FirstOrDefaultAsync(o => o.Id == userId);

                if (ogrenci == null)
                    return RedirectToAction("Login", "Account");

                ViewBag.Ogrenci = ogrenci;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yeni başvuru sayfası yüklenirken hata oluştu.");
                ViewBag.Error = "Sayfa yüklenirken hata oluştu.";
                return RedirectToAction("Index");
            }
        }

        // POST: Ogrenci/YeniBasvuru
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YeniBasvuru(string basvuruTuru, string aciklama, IFormFile? belgeDosyasi)
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(basvuruTuru) || string.IsNullOrEmpty(aciklama))
            {
                ViewBag.Error = "Başvuru türü ve açıklama alanları doldurulmalıdır.";
                await LoadBasvuruFormData(userId);
                return View();
            }

            try
            {
                // Mevcut başvuruyu kontrol et (OnayDetaylari ile birlikte yükle)
                var mevcutBasvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                if (mevcutBasvuru != null)
                {
                    if (mevcutBasvuru.Durum == ApplicationConstants.Beklemede)
                    {
                        ViewBag.Error = "Henüz işlemde olan başvurunuz bulunmaktadır. Lütfen mevcut başvurunuzun sonuçlanmasını bekleyiniz.";
                        await LoadBasvuruFormData(userId);
                        return View();
                    }
                    else if (mevcutBasvuru.Durum == ApplicationConstants.Onaylandi)
                    {
                        ViewBag.Error = "Onaylanmış başvurunuz bulunmaktadır. İlişik kesme işleminiz tamamlanmıştır.";
                        await LoadBasvuruFormData(userId);
                        return View();
                    }
                    // Reddedilen başvuru varsa, temizle
                    else if (mevcutBasvuru.Durum == ApplicationConstants.Reddedildi)
                    {
                        _logger.LogInformation($"POST: Reddedilen başvuru temizleniyor: {mevcutBasvuru.Id}");

                        // Transaction başlat (güvenli silme için)
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            // Önce OnayDetaylari'nı sil
                            if (mevcutBasvuru.OnayDetaylari != null && mevcutBasvuru.OnayDetaylari.Any())
                            {
                                _context.OnayDetaylari.RemoveRange(mevcutBasvuru.OnayDetaylari);
                                _logger.LogInformation($"POST: {mevcutBasvuru.OnayDetaylari.Count} adet OnayDetay siliniyor");
                            }

                            // Sonra Basvuru'yu sil
                            _context.Basvurular.Remove(mevcutBasvuru);

                            // Değişiklikleri kaydet
                            await _context.SaveChangesAsync();

                            // Transaction'ı onayla
                            await transaction.CommitAsync();

                            _logger.LogInformation($"POST: Reddedilen başvuru başarıyla temizlendi: {mevcutBasvuru.Id}");
                        }
                        catch (Exception deleteEx)
                        {
                            // Hata durumunda rollback
                            await transaction.RollbackAsync();
                            _logger.LogError(deleteEx, "POST: Reddedilen başvuru silinirken hata oluştu");
                            ViewBag.Error = "Önceki başvuru temizlenirken hata oluştu. Lütfen tekrar deneyiniz.";
                            await LoadBasvuruFormData(userId);
                            return View();
                        }
                    }
                }

                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Bolum)
                    .Include(o => o.Fakulte)
                    .FirstOrDefaultAsync(o => o.Id == userId);

                if (ogrenci == null)
                    return RedirectToAction("Login", "Account");

                // Dosya yükleme işlemi
                string? dosyaYolu = null;
                string? originalFileName = null;
                string? fileExtension = null;
                long? fileSize = null;
                string? contentType = null;

                if (belgeDosyasi != null && belgeDosyasi.Length > 0)
                {
                    var fileValidationResult = ValidateFile(belgeDosyasi);
                    if (!fileValidationResult.IsValid)
                    {
                        ViewBag.Error = fileValidationResult.ErrorMessage;
                        await LoadBasvuruFormData(userId);
                        return View();
                    }

                    try
                    {
                        var uploadResult = await SaveFileAsync(belgeDosyasi, ogrenci.OgrenciNo);
                        dosyaYolu = uploadResult.FilePath;
                        originalFileName = belgeDosyasi.FileName;
                        fileExtension = Path.GetExtension(belgeDosyasi.FileName).ToLowerInvariant();
                        fileSize = belgeDosyasi.Length;
                        contentType = belgeDosyasi.ContentType;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Dosya yükleme hatası.");
                        ViewBag.Error = "Dosya yüklenirken hata oluştu. Lütfen tekrar deneyiniz.";
                        await LoadBasvuruFormData(userId);
                        return View();
                    }
                }

                // Öğrenci türünü belirle
                var ogrenciTuru = ogrenci.Bolum?.EgitimTuru ?? "Lisans";

                var basvuru = new Basvuru
                {
                    OgrenciId = userId,
                    BasvuruTuru = basvuruTuru,
                    Aciklama = aciklama,
                    BasvuruTarihi = DateTime.Now,
                    Durum = ApplicationConstants.Beklemede,
                    MevcutAsama = 1,
                    SonGuncellemeTarihi = DateTime.Now,
                    // Dosya bilgileri
                    DosyaYolu = dosyaYolu,
                    OriginalDosyaAdi = originalFileName,
                    DosyaUzantisi = fileExtension,
                    DosyaBoyutu = fileSize,
                    DosyaContentType = contentType,
                    DosyaYuklemeTarihi = dosyaYolu != null ? DateTime.Now : null
                };

                // Toplam aşama sayısını hesapla
                var toplamAsama = await _context.OnayAsamalari
                    .CountAsync(oa => oa.OgrenciTuru == ogrenciTuru && oa.Aktif);

                basvuru.ToplamAsama = toplamAsama > 0 ? toplamAsama : 8;

                _context.Basvurular.Add(basvuru);
                await _context.SaveChangesAsync();

                await CreateOnayDetaylari(basvuru.Id, ogrenciTuru, ogrenci.FakulteId, ogrenci.BolumId);

                ViewBag.Success = "Başvurunuz başarıyla oluşturulmuştur." +
                                 (dosyaYolu != null ? " Belgeniz de yüklenmiştir." : "");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru oluşturulurken hata oluştu: {Message}", ex.Message);
                ViewBag.Error = "Başvuru oluşturulurken hata oluştu. Lütfen tekrar deneyiniz.";
                await LoadBasvuruFormData(userId);
                return View();
            }
        }

        // GET: Ogrenci/BasvuruTakip
        public async Task<IActionResult> BasvuruTakip()
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                if (basvuru == null)
                {
                    ViewBag.Error = "Başvurunuz bulunmamaktadır.";
                    return View();
                }

                // Yetkili adlarını güncelle
                foreach (var detay in basvuru.OnayDetaylari)
                {
                    detay.YetkiliAdi = detay.Yetkili?.AdSoyad ?? "Yetkili Atanmadı";
                }

                return View(basvuru);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru takip sayfası yüklenirken hata oluştu.");
                ViewBag.Error = "Başvuru takip sayfası yüklenirken hata oluştu.";
                return View();
            }
        }

        // GET: Ogrenci/IndirPDF
        public async Task<IActionResult> IndirPDF()
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                if (basvuru == null || basvuru.Durum != ApplicationConstants.Onaylandi)
                {
                    ViewBag.Error = "PDF indirilebilmesi için başvurunuzun onaylanmış olması gerekmektedir.";
                    return RedirectToAction("BasvuruTakip");
                }

                // Yetkili adlarını güncelle
                foreach (var detay in basvuru.OnayDetaylari)
                {
                    detay.YetkiliAdi = detay.Yetkili?.AdSoyad ?? "Yetkili Atanmadı";
                }

                var pdfBytes = GeneratePdf(basvuru);
                return File(pdfBytes, "application/pdf", $"IlisikKesmeFormu_{basvuru.Ogrenci.OgrenciNo}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF oluşturulurken hata oluştu.");
                ViewBag.Error = "PDF oluşturulurken hata oluştu.";
                return RedirectToAction("BasvuruTakip");
            }
        }

        // GET: Ogrenci/IndirBelge - Yüklenen belgeyi indir
        public async Task<IActionResult> IndirBelge()
        {
            if (!CheckOgrenciLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var basvuru = await _context.Basvurular
                    .FirstOrDefaultAsync(b => b.OgrenciId == userId);

                if (basvuru == null || string.IsNullOrEmpty(basvuru.DosyaYolu))
                {
                    ViewBag.Error = "İndirilecek belge bulunamadı.";
                    return RedirectToAction("Index");
                }

                var filePath = Path.Combine(_environment.WebRootPath, basvuru.DosyaYolu);

                if (!System.IO.File.Exists(filePath))
                {
                    ViewBag.Error = "Belge dosyası bulunamadı.";
                    return RedirectToAction("Index");
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var contentType = basvuru.DosyaContentType ?? "application/octet-stream";
                var fileName = basvuru.OriginalDosyaAdi ?? "belge" + basvuru.DosyaUzantisi;

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Belge indirme hatası.");
                ViewBag.Error = "Belge indirilirken hata oluştu.";
                return RedirectToAction("Index");
            }
        }

        // DOSYA UPLOAD HELPER METHODLARI

        private (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file)
        {
            // Dosya boyutu kontrolü
            if (file.Length > MaxFileSize)
            {
                return (false, $"Dosya boyutu çok büyük. Maksimum {MaxFileSize / (1024 * 1024)}MB yükleyebilirsiniz.");
            }

            if (file.Length == 0)
            {
                return (false, "Dosya boş olamaz.");
            }

            // Dosya uzantısı kontrolü
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return (false, $"Geçersiz dosya türü. İzin verilen türler: {string.Join(", ", AllowedExtensions)}");
            }

            // Content type kontrolü (ek güvenlik)
            var allowedContentTypes = new[]
            {
                "application/pdf",
                "image/jpeg",
                "image/jpg",
                "image/png",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };

            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return (false, "Geçersiz dosya içeriği.");
            }

            return (true, string.Empty);
        }

        private async Task<(string FilePath, string FileName)> SaveFileAsync(IFormFile file, string ogrenciNo)
        {
            // Upload klasörünü oluştur
            var uploadPath = Path.Combine(_environment.WebRootPath, UploadFolder);
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // Güvenli dosya adı oluştur
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{ogrenciNo}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            var filePath = Path.Combine(uploadPath, fileName);

            // Dosyayı kaydet
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Relative path döndür
            var relativePath = Path.Combine(UploadFolder, fileName).Replace("\\", "/");

            _logger.LogInformation($"Dosya kaydedildi: {relativePath}");

            return (relativePath, fileName);
        }

        // Yardımcı metodlar
        private async Task LoadBasvuruFormData(int userId)
        {
            try
            {
                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .FirstOrDefaultAsync(o => o.Id == userId);

                ViewBag.Ogrenci = ogrenci;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru formu verileri yüklenirken hata oluştu.");
            }
        }

        private async Task CreateOnayDetaylari(int basvuruId, string ogrenciTuru, int fakulteId, int bolumId)
        {
            try
            {
                var onayAsamalari = await _context.OnayAsamalari
                    .Where(oa => oa.OgrenciTuru == ogrenciTuru && oa.Aktif)
                    .OrderBy(oa => oa.SiraNo)
                    .ToListAsync();

                if (!onayAsamalari.Any())
                {
                    _logger.LogWarning($"{ogrenciTuru} için onay aşaması tanımlanmamış.");
                    throw new InvalidOperationException($"{ogrenciTuru} için onay aşaması tanımlanmamış.");
                }

                var onayDetaylari = new List<OnayDetay>();

                foreach (var asama in onayAsamalari)
                {
                    var atamaYetkili = await GetOptimalYetkili(asama, fakulteId, bolumId);

                    var onayDetay = new OnayDetay
                    {
                        BasvuruId = basvuruId,
                        AsamaNo = asama.SiraNo,
                        AsamaAdi = asama.AsamaAdi,
                        Durum = ApplicationConstants.Beklemede,
                        YetkiliId = atamaYetkili?.Id,
                        YetkiliAdi = atamaYetkili?.AdSoyad ?? "Yetkili Atanmadı",
                        BeklenenFakulteId = asama.Ortak ? null : fakulteId,
                        YetkiliPozisyonu = asama.YetkiliPozisyonu,
                        OlusturmaTarihi = DateTime.Now
                    };

                    onayDetaylari.Add(onayDetay);
                }

                _context.OnayDetaylari.AddRange(onayDetaylari);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Başvuru ID {basvuruId} için {onayDetaylari.Count} onay aşaması oluşturuldu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnayDetaylari oluşturma hatası: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<Yetkili?> GetOptimalYetkili(OnayAsama asama, int fakulteId, int bolumId)
        {
            try
            {
                var query = _context.Yetkililer
                    .Where(y => y.OnayAsamasi == asama.AsamaAdi &&
                               y.Aktif &&
                               !y.OnayBekliyor);

                if (asama.Ortak)
                {
                    // Ortak aşama: En az yüklü yetkiliye ata
                    return await GetLeastBusyAuthority(query);
                }
                else if (asama.BolumBazli)
                {
                    // Bölüm bazlı aşama: İlgili bölümdeki yetkiliyi bul
                    var bolumYetkili = await query
                        .FirstOrDefaultAsync(y => y.FakulteId == fakulteId && y.BolumId == bolumId);

                    if (bolumYetkili != null)
                    {
                        _logger.LogInformation($"[BÖLÜM YETKİLİSİ] {asama.AsamaAdi} -> {bolumYetkili.AdSoyad} (Bölüm: {bolumId})");
                        return bolumYetkili;
                    }

                    _logger.LogWarning($"[UYARI] {asama.AsamaAdi} için Bölüm {bolumId}'de yetkili bulunamadı!");
                    return null;
                }
                else if (asama.FakulteBazli)
                {
                    // Fakülte bazlı aşama: İlgili fakültedeki yetkililerden en az yüklüyü seç
                    var fakulteQuery = query.Where(y => y.FakulteId == fakulteId);
                    var fakulteYetkili = await GetLeastBusyAuthority(fakulteQuery);

                    if (fakulteYetkili != null)
                    {
                        _logger.LogInformation($"[FAKÜLTE YETKİLİSİ] {asama.AsamaAdi} -> {fakulteYetkili.AdSoyad} (Fakülte: {fakulteId})");
                        return fakulteYetkili;
                    }

                    _logger.LogWarning($"[UYARI] {asama.AsamaAdi} için Fakülte {fakulteId}'de yetkili bulunamadı!");
                    return null;
                }
                else
                {
                    // Genel aşama: İlk uygun yetkiliye ata
                    return await query.FirstOrDefaultAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili atama hatası ({AsamaAdi}): {Message}", asama.AsamaAdi, ex.Message);
                return null;
            }
        }

        private async Task<Yetkili?> GetLeastBusyAuthority(IQueryable<Yetkili> query)
        {
            try
            {
                var yetkilileriVeYukleri = await query
                    .Select(y => new
                    {
                        Yetkili = y,
                        BekleyenBasvuruSayisi = _context.OnayDetaylari
                            .Count(od => od.YetkiliId == y.Id && od.Durum == ApplicationConstants.Beklemede)
                    })
                    .OrderBy(x => x.BekleyenBasvuruSayisi)
                    .ThenBy(x => x.Yetkili.SonAktiviteTarihi ?? DateTime.MinValue)
                    .FirstOrDefaultAsync();

                if (yetkilileriVeYukleri != null)
                {
                    _logger.LogInformation($"[EN AZ YÜKLÜ] {yetkilileriVeYukleri.Yetkili.AdSoyad} - Bekleyen: {yetkilileriVeYukleri.BekleyenBasvuruSayisi}");
                    return yetkilileriVeYukleri.Yetkili;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "En az yüklü yetkili bulma hatası.");
                return await query.FirstOrDefaultAsync(); // Fallback
            }
        }

        private byte[] GeneratePdf(Basvuru basvuru)
        {
            try
            {
                var document = Document.Create(doc =>
                {
                    doc.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12));

                        page.Header()
                            .Text("İlişik Kesme Formu")
                            .SemiBold().FontSize(36).AlignCenter();

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(x =>
                            {
                                x.Item().Text("Öğrenci Bilgileri").FontSize(16).SemiBold();
                                x.Item().Column(col =>
                                {
                                    col.Spacing(5);
                                    col.Item().Text($"Ad Soyad: {basvuru.Ogrenci.TamAd}");
                                    col.Item().Text($"Öğrenci No: {basvuru.Ogrenci.OgrenciNo}");
                                    col.Item().Text($"Fakülte: {basvuru.Ogrenci.Fakulte.FakulteAdi}");
                                    col.Item().Text($"Bölüm: {basvuru.Ogrenci.Bolum.BolumAdi}");
                                    col.Item().Text($"Başvuru Türü: {basvuru.BasvuruTuru}");
                                    col.Item().Text($"Açıklama: {basvuru.Aciklama}");
                                    col.Item().Text($"Başvuru Tarihi: {basvuru.BasvuruTarihi:dd/MM/yyyy}");
                                    col.Item().Text($"Onay Tarihi: {basvuru.TamamlanmaTarihi?.ToString("dd/MM/yyyy") ?? "-"}");

                                    // Dosya bilgisi varsa ekle
                                    if (basvuru.HasFile)
                                    {
                                        col.Item().Text($"Yüklenen Belge: {basvuru.OriginalDosyaAdi}");
                                        col.Item().Text($"Belge Boyutu: {basvuru.DosyaBoyutuText}");
                                        col.Item().Text($"Yükleme Tarihi: {basvuru.DosyaYuklemeTarihi?.ToString("dd/MM/yyyy HH:mm") ?? "-"}");
                                    }
                                });

                                x.Item().PaddingTop(20).Text("Onay Aşamaları").FontSize(16).SemiBold();
                                x.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(30);
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                    });

                                    // Header
                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("#").SemiBold();
                                        header.Cell().Element(CellStyle).Text("Aşama").SemiBold();
                                        header.Cell().Element(CellStyle).Text("Yetkili").SemiBold();
                                        header.Cell().Element(CellStyle).Text("Durum").SemiBold();
                                        header.Cell().Element(CellStyle).Text("Tarih").SemiBold();
                                    });

                                    // Rows
                                    foreach (var detay in basvuru.OnayDetaylari.OrderBy(od => od.AsamaNo))
                                    {
                                        table.Cell().Element(CellStyle).Text($"{detay.AsamaNo}");
                                        table.Cell().Element(CellStyle).Text(detay.AsamaAdi);
                                        table.Cell().Element(CellStyle).Text(detay.YetkiliAdi);
                                        table.Cell().Element(CellStyle).Text(detay.Durum);
                                        table.Cell().Element(CellStyle).Text(detay.OnayTarihi?.ToString("dd/MM/yyyy") ?? "-");
                                    }
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Sayfa ");
                                x.CurrentPageNumber();
                                x.Span(" / ");
                                x.TotalPages();
                                x.Span($" - {DateTime.Now:dd/MM/yyyy HH:mm}");
                            });
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF oluşturma hatası.");
                throw new InvalidOperationException("PDF oluşturulamadı.", ex);
            }
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
        }
    }
}