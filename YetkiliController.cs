using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HayataAtilmaFormu.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace HayataAtilmaFormu.Controllers
{
    public class YetkiliController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YetkiliController> _logger;
        private readonly IEmailService _emailService; // E-posta servisi eklendi

        public YetkiliController(ApplicationDbContext context, ILogger<YetkiliController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService; // Dependency injection
        }

        // Yetkili girişi kontrolü
        private bool CheckYetkiliLogin()
        {
            var userType = HttpContext.Session.GetString("UserType");
            var userId = HttpContext.Session.GetString("UserId");

            return (userType == ApplicationConstants.YetkiliRole || userType == ApplicationConstants.SuperAdminRole)
                   && !string.IsNullOrEmpty(userId);
        }

        // GET: Yetkili/Index
        public async Task<IActionResult> Index()
        {
            if (!CheckYetkiliLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                _logger.LogWarning("Geçersiz UserId formatı.");
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(HttpContext.Session.GetString("FakulteId"), out var fakulteId))
            {
                _logger.LogWarning("Geçersiz FakulteId formatı.");
                return RedirectToAction("Login", "Account");
            }

            var onayAsamasi = HttpContext.Session.GetString("OnayAsamasi");

            try
            {
                // Yetkili bilgilerini getir
                var yetkili = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .Include(y => y.Bolum)
                    .FirstOrDefaultAsync(y => y.Id == userId);

                if (yetkili == null)
                {
                    _logger.LogWarning("Yetkili bulunamadı.");
                    return RedirectToAction("Login", "Account");
                }

                // Bu yetkilinin onayını bekleyen başvuruları getir
                var bekleyenBasvurular = await GetBekleyenBasvurular(fakulteId, onayAsamasi, userId, yetkili.BolumId);

                // İstatistikler
                var stats = await GetDetayliIstatistikler(userId, fakulteId, yetkili.BolumId, onayAsamasi);

                ViewBag.Yetkili = yetkili;
                ViewBag.BekleyenBasvurular = bekleyenBasvurular;
                ViewBag.Stats = stats;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili index yüklenirken hata oluştu.");
                ViewBag.Error = "Hata oluştu: " + ex.Message;
                return View();
            }
        }
        public async Task<IActionResult> Basvurular(string durum = "Tumu", int sayfa = 1, int sayfaBoyutu = 20)
        {
            if (!CheckYetkiliLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(HttpContext.Session.GetString("FakulteId"), out var fakulteId))
            {
                return RedirectToAction("Login", "Account");
            }

            var onayAsamasi = HttpContext.Session.GetString("OnayAsamasi");

            try
            {
                // Bu yetkilinin bilgilerini al
                var yetkili = await _context.Yetkililer
                    .FirstOrDefaultAsync(y => y.Id == userId);

                if (yetkili == null)
                {
                    return RedirectToAction("Login", "Account");
                }
                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi);

                var query = BuildBasvuruQuery(onayAsamasi, fakulteId, userId, yetkili.BolumId, onayAsama);

                // Durum filtreleme
                query = ApplyStatusFilter(query, durum, userId);

                var toplamKayit = await query.CountAsync();
                var basvurular = await query
                    .OrderByDescending(od => od.Basvuru.BasvuruTarihi)
                    .Skip((sayfa - 1) * sayfaBoyutu)
                    .Take(sayfaBoyutu)
                    .ToListAsync();

                ViewBag.Durum = durum;
                ViewBag.MevcutSayfa = sayfa;
                ViewBag.ToplamSayfa = (int)Math.Ceiling((double)toplamKayit / sayfaBoyutu);
                ViewBag.ToplamKayit = toplamKayit;

                return View(basvurular);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvurular yüklenirken hata oluştu.");
                ViewBag.Error = "Başvurular yüklenirken hata oluştu.";
                return View(new List<OnayDetay>());
            }
        }
        public async Task<IActionResult> BasvuruDetay(int id)
        {
            if (!CheckYetkiliLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(HttpContext.Session.GetString("FakulteId"), out var fakulteId))
            {
                return RedirectToAction("Login", "Account");
            }

            var onayAsamasi = HttpContext.Session.GetString("OnayAsamasi");

            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (basvuru == null)
                {
                    ViewBag.Error = "Başvuru bulunamadı.";
                    return RedirectToAction("Basvurular");
                }

                if (!CheckBasvuruAccess(basvuru, userId, fakulteId, onayAsamasi))
                {
                    ViewBag.Error = "Bu başvuruya erişim yetkiniz yok.";
                    return RedirectToAction("Basvurular");
                }

                foreach (var detay in basvuru.OnayDetaylari)
                {
                    detay.YetkiliAdi = detay.Yetkili?.AdSoyad ?? "Yetkili Atanmadı";
                }

                // GELIŞTIRILMIŞ ONAY SIRASI KONTROLÜ
                var onayKontrolSonucu = await CheckOnayEligibility(basvuru, userId, onayAsamasi);

                ViewBag.CurrentYetkiliId = userId;
                ViewBag.CurrentAsama = onayAsamasi;
                ViewBag.CanApprove = onayKontrolSonucu.CanApprove; // Geliştirilmiş kontrol
                ViewBag.ApprovalMessage = onayKontrolSonucu.Message; // Kullanıcıya gösterilecek mesaj
                ViewBag.MyDecision = onayKontrolSonucu.MyDecision;
                ViewBag.IsMyStage = onayKontrolSonucu.IsMyStage;

                return View(basvuru);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru detayları yüklenirken hata oluştu.");
                ViewBag.Error = "Başvuru detayları yüklenirken hata oluştu.";
                return RedirectToAction("Basvurular");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnayVer(int basvuruId, string karar, string aciklama)
        {
            try
            {
                _logger.LogInformation("OnayVer çağrıldı - basvuruId: {basvuruId}, karar: {karar}, aciklama: {aciklama}",
                    basvuruId, karar, aciklama);

                if (!CheckYetkiliLogin())
                {
                    return Json(new { success = false, message = "Oturum süreniz dolmuş. Lütfen tekrar giriş yapın." });
                }

                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                {
                    return Json(new { success = false, message = "Geçersiz kullanıcı kimliği." });
                }

                if (!int.TryParse(HttpContext.Session.GetString("FakulteId"), out var fakulteId))
                {
                    return Json(new { success = false, message = "Geçersiz fakülte kimliği." });
                }

                var onayAsamasi = HttpContext.Session.GetString("OnayAsamasi");

                // Validation kontrollerini güçlendir
                if (basvuruId <= 0)
                {
                    return Json(new { success = false, message = "Geçersiz başvuru kimliği." });
                }

                if (string.IsNullOrWhiteSpace(karar))
                {
                    return Json(new { success = false, message = "Karar boş olamaz." });
                }

                if (karar != ApplicationConstants.Onaylandi && karar != ApplicationConstants.Reddedildi)
                {
                    return Json(new { success = false, message = "Geçersiz karar türü." });
                }

                if (karar == ApplicationConstants.Reddedildi && string.IsNullOrWhiteSpace(aciklama))
                {
                    return Json(new { success = false, message = "Red durumunda açıklama zorunludur." });
                }

                if (!string.IsNullOrEmpty(aciklama) && aciklama.Length > 500)
                {
                    return Json(new { success = false, message = "Açıklama çok uzun (maksimum 500 karakter)." });
                }

                // ÖNEMLİ: Önce başvuruyu getir ve yetki kontrolü yap
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                // Yetki kontrolü
                var onayKontrolSonucu = await CheckOnayEligibility(basvuru, userId, onayAsamasi);
                if (!onayKontrolSonucu.CanApprove)
                {
                    return Json(new { success = false, message = onayKontrolSonucu.Message });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await ProcessApprovalAsync(basvuruId, userId, fakulteId, onayAsamasi, karar, aciklama);

                    if (result.Success)
                    {
                        await transaction.CommitAsync();

                        // E-POSTA GÖNDERME İŞLEMİ - İyileştirilmiş
                        try
                        {
                            // Güncellenmiş başvuru bilgisini tekrar getir
                            var updatedBasvuru = await _context.Basvurular
                                .Include(b => b.Ogrenci)
                                    .ThenInclude(o => o.Fakulte)
                                .Include(b => b.Ogrenci)
                                    .ThenInclude(o => o.Bolum)
                                .Include(b => b.OnayDetaylari)
                                .FirstOrDefaultAsync(b => b.Id == basvuruId);

                            if (updatedBasvuru != null)
                            {
                                // Başvuru tamamen onaylandı mı kontrol et
                                if (updatedBasvuru.Durum == ApplicationConstants.Onaylandi)
                                {
                                    await _emailService.SendBasvuruOnayEmailAsync(updatedBasvuru);
                                    _logger.LogInformation("Onay e-postası gönderildi: {Email}", updatedBasvuru.Ogrenci.Email);
                                }
                                // Başvuru reddedildi mi kontrol et
                                else if (updatedBasvuru.Durum == ApplicationConstants.Reddedildi)
                                {
                                    await _emailService.SendBasvuruRedEmailAsync(updatedBasvuru, updatedBasvuru.RedNedeni ?? aciklama);
                                    _logger.LogInformation("Red e-postası gönderildi: {Email}", updatedBasvuru.Ogrenci.Email);
                                }
                                // Ara aşama - Durum bildirimi gönderme (İsteğe bağlı)
                                else if (karar == ApplicationConstants.Onaylandi)
                                {
                                    // Ara onay bildirimi göndermek isterseniz burayı aktifleştirin
                                    // await _emailService.SendBasvuruDurumBildirimiAsync(
                                    //     updatedBasvuru.Ogrenci.Email,
                                    //     updatedBasvuru.Ogrenci.TamAd,
                                    //     updatedBasvuru.Ogrenci.OgrenciNo,
                                    //     updatedBasvuru.BasvuruTuru,
                                    //     "Ara Onay Alındı"
                                    // );
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            // E-posta gönderilemezse işlem yine de devam etsin
                            _logger.LogError(emailEx, "E-posta gönderme hatası - İşlem devam ediyor");
                            // E-posta hatası kullanıcıya gösterilmeyebilir, sadece log'a yazılır
                        }

                        return Json(new
                        {
                            success = true,
                            message = karar == ApplicationConstants.Onaylandi ?
                                "Başvuru başarıyla onaylandı." : "Başvuru başarıyla reddedildi.",
                            redirectUrl = Url.Action("Basvurular")
                        });
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = result.ErrorMessage });
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Onay işlemi sırasında hata oluştu.");
                    return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onay işlemi sırasında genel hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
            }
        }
        private async Task<(bool CanApprove, string Message, bool IsMyStage, string? MyDecision)> CheckOnayEligibility(Basvuru basvuru, int userId, string? onayAsamasi)
        {
            try
            {
                // Mevcut yetkili için bu aşamada onay detayı var mı?
                var benimOnayDetayim = basvuru.OnayDetaylari
                    .FirstOrDefault(od => od.AsamaAdi == onayAsamasi &&
                                        (od.YetkiliId == userId || od.YetkiliId == null));

                // Eğer bu aşamada yetkili değilsem
                if (benimOnayDetayim == null)
                {
                    return (false, "Bu başvuru sizin onay aşamanızda değil.", false, null);
                }

                // Eğer zaten karar vermişsem
                if (benimOnayDetayim.Durum != ApplicationConstants.Beklemede)
                {
                    var kararim = benimOnayDetayim.Durum == ApplicationConstants.Onaylandi ? "onayladınız" : "reddettiniz";
                    return (false, $"Bu başvuruyu zaten {kararim}.", true, benimOnayDetayim.Durum);
                }

                // Başvuru mevcut aşamada mı?
                if (basvuru.MevcutAsama != benimOnayDetayim.AsamaNo)
                {
                    return (false, "Bu başvuru henüz sizin aşamanıza gelmedi.", true, null);
                }

                // Önceki aşamalar tamamlandı mı?
                var oncekiTamamlanmamisAsama = basvuru.OnayDetaylari
                    .Where(od => od.AsamaNo < basvuru.MevcutAsama && od.Durum == ApplicationConstants.Beklemede)
                    .OrderBy(od => od.AsamaNo)
                    .FirstOrDefault();

                if (oncekiTamamlanmamisAsama != null)
                {
                    return (false, $"Önce {oncekiTamamlanmamisAsama.AsamaAdi} aşaması tamamlanmalıdır.", true, null);
                }

                // Yetkili ataması kontrolü
                var yetkili = await _context.Yetkililer.FirstOrDefaultAsync(y => y.Id == userId);
                if (yetkili == null)
                {
                    return (false, "Yetkili bilgileri bulunamadı.", true, null);
                }

                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi);

                if (onayAsama != null)
                {
                    // Bölüm bazlı aşama kontrolü
                    if (onayAsama.BolumBazli && yetkili.BolumId.HasValue)
                    {
                        if (basvuru.Ogrenci.BolumId != yetkili.BolumId.Value)
                        {
                            return (false, "Bu başvuru sizin bölümünüze ait değil.", false, null);
                        }
                    }
                    // Fakülte bazlı aşama kontrolü
                    else if (onayAsama.FakulteBazli)
                    {
                        if (basvuru.Ogrenci.FakulteId != yetkili.FakulteId)
                        {
                            return (false, "Bu başvuru sizin fakültenize ait değil.", false, null);
                        }
                    }
                }

                // Her şey yolunda, onay verebilir
                return (true, "Bu başvuruyu onaylayabilir veya reddedebilirsiniz.", true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckOnayEligibility kontrolü sırasında hata oluştu.");
                return (false, "Yetki kontrolü sırasında hata oluştu.", false, null);
            }
        }

        // GET: Yetkili/Istatistikler
        public async Task<IActionResult> Istatistikler()
        {
            if (!CheckYetkiliLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!int.TryParse(HttpContext.Session.GetString("FakulteId"), out var fakulteId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var yetkili = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .FirstOrDefaultAsync(y => y.Id == userId);

                if (yetkili == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var onayAsamasi = HttpContext.Session.GetString("OnayAsamasi");
                var stats = await GetDetayliIstatistikler(userId, fakulteId, yetkili.BolumId, onayAsamasi);

                ViewBag.Stats = stats;
                ViewBag.Yetkili = yetkili;

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler yüklenirken hata oluştu.");
                ViewBag.Error = "İstatistikler yüklenirken hata oluştu.";
                return View();
            }
        }

        // AJAX: Filtrelenmiş istatistikleri getir
        [HttpPost]
        public async Task<JsonResult> GetFilteredStats(string dateFilter, string typeFilter)
        {
            try
            {
                if (!CheckYetkiliLogin())
                    return Json(new { success = false, message = "Yetkisiz erişim." });

                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                    return Json(new { success = false, message = "Geçersiz kullanıcı kimliği." });

                DateTime? startDate = null;
                switch (dateFilter)
                {
                    case "thisMonth":
                        startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                        break;
                    case "last3Months":
                        startDate = DateTime.Now.AddMonths(-3);
                        break;
                    case "thisYear":
                        startDate = new DateTime(DateTime.Now.Year, 1, 1);
                        break;
                    case "allTime":
                        startDate = null;
                        break;
                }

                var query = _context.OnayDetaylari
                    .Include(od => od.Basvuru)
                    .Where(od => od.YetkiliId == userId);

                if (startDate.HasValue)
                {
                    query = query.Where(od => od.OnayTarihi >= startDate);
                }

                if (typeFilter != "all")
                {
                    query = query.Where(od => od.Basvuru.BasvuruTuru == typeFilter);
                }

                var stats = await query.GroupBy(od => od.Durum).Select(g => new
                {
                    Durum = g.Key,
                    Count = g.Count()
                }).ToListAsync();

                var toplamBasvuru = stats.Sum(s => s.Count);
                var onaylanan = stats.FirstOrDefault(s => s.Durum == ApplicationConstants.Onaylandi)?.Count ?? 0;
                var reddedilen = stats.FirstOrDefault(s => s.Durum == ApplicationConstants.Reddedildi)?.Count ?? 0;

                var yanitSureleri = await query
                    .Where(od => od.OnayTarihi.HasValue)
                    .Select(od => EF.Functions.DateDiffDay(od.Basvuru.BasvuruTarihi, od.OnayTarihi.Value))
                    .ToListAsync();

                var ortalamaGun = yanitSureleri.Any() ? yanitSureleri.Average() : 0;

                return Json(new
                {
                    success = true,
                    toplamBasvuru,
                    onaylananBasvuru = onaylanan,
                    reddettigimBasvuru = reddedilen,
                    ortalamaGun = Math.Round(ortalamaGun, 1)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Filtrelenmiş istatistikler yüklenirken hata oluştu.");
                return Json(new { success = false, message = "Bir hata oluştu." });
            }
        }

        // Excel export
        public async Task<IActionResult> ExportStats()
        {
            if (!CheckYetkiliLogin())
                return RedirectToAction("Login", "Account");

            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var data = await _context.OnayDetaylari
                    .Include(od => od.Basvuru)
                        .ThenInclude(b => b.Ogrenci)
                            .ThenInclude(o => o.Bolum)
                    .Where(od => od.YetkiliId == userId)
                    .Select(od => new
                    {
                        OgrenciAdi = od.Basvuru.Ogrenci.TamAd,
                        OgrenciNo = od.Basvuru.Ogrenci.OgrenciNo,
                        BolumAdi = od.Basvuru.Ogrenci.Bolum.BolumAdi,
                        BasvuruTuru = od.Basvuru.BasvuruTuru,
                        Durum = od.Durum,
                        IslemTarihi = od.OnayTarihi
                    })
                    .OrderByDescending(x => x.IslemTarihi)
                    .ToListAsync();

                var csv = "Öğrenci Adı,Öğrenci No,Bölüm,Başvuru Türü,Durum,İşlem Tarihi\n";
                foreach (var item in data)
                {
                    csv += $"{item.OgrenciAdi},{item.OgrenciNo},{item.BolumAdi},{item.BasvuruTuru},{item.Durum},{item.IslemTarihi:dd/MM/yyyy HH:mm}\n";
                }

                var bytes = Encoding.UTF8.GetBytes(csv);
                return File(bytes, "text/csv", "yetkili_istatistikleri.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistik dışa aktarımı sırasında hata oluştu.");
                ViewBag.Error = "İstatistik dışa aktarımı sırasında hata oluştu.";
                return RedirectToAction("Istatistikler");
            }
        }

        // AJAX: Fakulteye göre bölümleri getir
        [HttpGet]
        public async Task<JsonResult> GetBolumlerByFakulte(int fakulteId)
        {
            try
            {
                if (fakulteId <= 0)
                {
                    return Json(new { success = false, message = "Geçersiz fakülte ID." });
                }

                var bolumler = await _context.Bolumler
                    .Where(b => b.FakulteId == fakulteId && b.Aktif)
                    .OrderBy(b => b.BolumAdi)
                    .Select(b => new
                    {
                        id = b.Id,
                        bolumAdi = b.BolumAdi
                    })
                    .ToListAsync();

                return Json(bolumler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bölüm yükleme hatası.");
                return Json(new { success = false, message = "Bölümler yüklenirken hata oluştu." });
            }
        }

        // Yardımcı metodlar
        private IQueryable<OnayDetay> BuildBasvuruQuery(string? onayAsamasi, int fakulteId, int userId, int? bolumId, OnayAsama? onayAsama)
        {
            var query = _context.OnayDetaylari
                .Include(od => od.Basvuru)
                    .ThenInclude(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                .Include(od => od.Basvuru)
                    .ThenInclude(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                .Where(od => od.AsamaAdi == onayAsamasi);

            // Aşama türüne göre filtreleme uygula
            if (onayAsama != null)
            {
                if (onayAsama.Ortak)
                {
                    // Ortak aşama: Sadece bu yetkili
                    query = query.Where(od => od.YetkiliId == userId);
                }
                else if (onayAsama.BolumBazli && bolumId.HasValue)
                {
                    // DÜZELTME: Bölüm bazlı aşama - Sadece yetkililerin kendi bölümündeki öğrenciler
                    query = query.Where(od => od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                            od.Basvuru.Ogrenci.BolumId == bolumId.Value &&
                                            (od.YetkiliId == userId || od.YetkiliId == null));
                }
                else if (onayAsama.FakulteBazli)
                {
                    // Fakülte bazlı aşama: Sadece kendi fakültesi
                    query = query.Where(od => od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                            (od.YetkiliId == userId || od.YetkiliId == null));
                }
                else
                {
                    // Diğer durumlar: Sadece bu yetkili
                    query = query.Where(od => od.YetkiliId == userId);
                }
            }
            else
            {
                query = query.Where(od => od.YetkiliId == userId);
            }

            return query;
        }

        private IQueryable<OnayDetay> ApplyStatusFilter(IQueryable<OnayDetay> query, string durum, int userId)
        {
            return durum.ToLower() switch
            {
                "beklemede" => query.Where(od => od.Durum == ApplicationConstants.Beklemede &&
                                               od.Basvuru.MevcutAsama == od.AsamaNo),
                "onaylandi" => query.Where(od => od.Durum == ApplicationConstants.Onaylandi && od.YetkiliId == userId),
                "reddedildi" => query.Where(od => od.Durum == ApplicationConstants.Reddedildi && od.YetkiliId == userId),
                _ => query
            };
        }

        private bool CheckBasvuruAccess(Basvuru basvuru, int userId, int fakulteId, string? onayAsamasi)
        {
            // Fakülte kontrolü
            if (basvuru.Ogrenci.FakulteId != fakulteId)
                return false;

            // Aşama kontrolü
            var hasRelevantStage = basvuru.OnayDetaylari.Any(od => od.AsamaAdi == onayAsamasi);
            if (!hasRelevantStage)
                return false;

            return true;
        }

        private async Task<(bool Success, string ErrorMessage)> ProcessApprovalAsync(int basvuruId, int userId, int fakulteId, string? onayAsamasi, string karar, string? aciklama)
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                    .FirstOrDefaultAsync(b => b.Id == basvuruId && b.Ogrenci.FakulteId == fakulteId);

                if (basvuru == null)
                {
                    return (false, "Başvuru bulunamadı veya erişim yetkiniz yok.");
                }

                // Sıralı onay kontrolü
                var oncekiTamamlanmamisAsama = basvuru.OnayDetaylari
                    .Where(od => od.AsamaNo < basvuru.MevcutAsama && od.Durum == ApplicationConstants.Beklemede)
                    .OrderBy(od => od.AsamaNo)
                    .FirstOrDefault();

                if (oncekiTamamlanmamisAsama != null)
                {
                    return (false, $"Önce {oncekiTamamlanmamisAsama.AsamaAdi} aşaması tamamlanmalıdır.");
                }

                var onayDetay = basvuru.OnayDetaylari
                    .FirstOrDefault(od => od.AsamaAdi == onayAsamasi &&
                                        od.Durum == ApplicationConstants.Beklemede &&
                                        od.AsamaNo == basvuru.MevcutAsama);

                if (onayDetay == null)
                {
                    return (false, "Bu aşamada size ait onaylanacak bir işlem bulunamadı.");
                }

                // Yetkili bilgilerini al
                var yetkili = await _context.Yetkililer.FindAsync(userId);
                if (yetkili == null)
                {
                    return (false, "Yetkili bilgileri bulunamadı.");
                }

                // Onay detayını güncelle
                onayDetay.YetkiliId = userId;
                onayDetay.YetkiliAdi = yetkili.AdSoyad;
                onayDetay.Durum = karar;
                onayDetay.OnayTarihi = DateTime.Now;
                onayDetay.Aciklama = aciklama ?? "";

                // Yetkili istatistiklerini güncelle
                if (karar == ApplicationConstants.Onaylandi)
                {
                    yetkili.ToplamOnayladigiBasvuru++;
                    await HandleApproval(basvuru);
                }
                else if (karar == ApplicationConstants.Reddedildi)
                {
                    yetkili.ToplamReddettigiBasvuru++;
                    HandleRejection(basvuru, aciklama);
                }

                // Ortalama cevap süresini güncelle
                var cevapSuresi = (DateTime.Now - basvuru.BasvuruTarihi).TotalDays;
                if (yetkili.OrtalamaCevapSuresi.HasValue)
                {
                    yetkili.OrtalamaCevapSuresi = (yetkili.OrtalamaCevapSuresi.Value + cevapSuresi) / 2;
                }
                else
                {
                    yetkili.OrtalamaCevapSuresi = cevapSuresi;
                }

                yetkili.SonAktiviteTarihi = DateTime.Now;
                basvuru.SonGuncellemeTarihi = DateTime.Now;
                basvuru.SonIslemYapanYetkiliId = userId;

                await _context.SaveChangesAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessApprovalAsync içinde hata: {Message}", ex.Message);
                return (false, "İşlem sırasında veritabanı hatası oluştu.");
            }
        }

        private async Task HandleApproval(Basvuru basvuru)
        {
            var sonrakiAsama = basvuru.OnayDetaylari
                .Where(od => od.AsamaNo > basvuru.MevcutAsama && od.Durum == ApplicationConstants.Beklemede)
                .OrderBy(od => od.AsamaNo)
                .FirstOrDefault();

            if (sonrakiAsama != null)
            {
                basvuru.MevcutAsama = sonrakiAsama.AsamaNo;
                basvuru.Durum = ApplicationConstants.Beklemede;
            }
            else
            {
                basvuru.Durum = ApplicationConstants.Onaylandi;
                basvuru.TamamlanmaTarihi = DateTime.Now;
                basvuru.MevcutAsama = basvuru.ToplamAsama;

                // E-posta gönder
                try
                {
                    await _emailService.SendBasvuruDurumBildirimiAsync(
                        basvuru.Ogrenci.Email,
                        basvuru.Ogrenci.TamAd,
                        basvuru.Ogrenci.OgrenciNo,
                        basvuru.BasvuruTuru,
                        ApplicationConstants.Onaylandi
                    );
                    _logger.LogInformation("Onay e-postası gönderildi: {Email}", basvuru.Ogrenci.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Onay e-postası gönderilemedi: {Email}", basvuru.Ogrenci.Email);
                }
            }
        }

        private void HandleRejection(Basvuru basvuru, string? aciklama)
        {
            basvuru.Durum = ApplicationConstants.Reddedildi;
            basvuru.RedNedeni = aciklama ?? "";
            basvuru.TamamlanmaTarihi = DateTime.Now;

            // Diğer bekleyen aşamaları iptal et
            var bekleyenAsamalar = basvuru.OnayDetaylari
                .Where(od => od.Durum == ApplicationConstants.Beklemede);

            foreach (var bekleyenAsama in bekleyenAsamalar)
            {
                if (bekleyenAsama.AsamaNo != basvuru.MevcutAsama)
                {
                    bekleyenAsama.Durum = ApplicationConstants.Iptal;
                }
            }
        }

        private async Task<List<OnayDetay>> GetBekleyenBasvurular(int fakulteId, string? onayAsamasi, int yetkiliId, int? bolumId)
        {
            try
            {
                var yetkili = await _context.Yetkililer.FirstOrDefaultAsync(y => y.Id == yetkiliId);
                if (yetkili == null) return new List<OnayDetay>();

                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi);

                var query = _context.OnayDetaylari
                    .Include(od => od.Basvuru)
                        .ThenInclude(b => b.Ogrenci)
                            .ThenInclude(o => o.Fakulte)
                    .Include(od => od.Basvuru)
                        .ThenInclude(b => b.Ogrenci)
                            .ThenInclude(o => o.Bolum)
                    .Include(od => od.Yetkili)
                    .Where(od => od.AsamaAdi == onayAsamasi &&
                               od.Durum == ApplicationConstants.Beklemede &&
                               od.Basvuru.MevcutAsama == od.AsamaNo &&
                               !od.Basvuru.OnayDetaylari.Any(prev => prev.AsamaNo < od.AsamaNo && prev.Durum == ApplicationConstants.Beklemede));

                // Aşama türüne göre filtreleme
                if (onayAsama != null)
                {
                    if (onayAsama.Ortak)
                    {
                        query = query.Where(od => od.YetkiliId == yetkiliId);
                    }
                    else if (onayAsama.BolumBazli && yetkili.BolumId.HasValue)
                    {
                        // DÜZELTME: Bölüm bazlı aşama - Yetkililerin BolumId'si ile öğrencinin BolumId'si eşleşmeli
                        query = query.Where(od => od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                                od.Basvuru.Ogrenci.BolumId == yetkili.BolumId.Value &&
                                                (od.YetkiliId == yetkiliId || od.YetkiliId == null));
                    }
                    else if (onayAsama.FakulteBazli)
                    {
                        query = query.Where(od => od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                                (od.YetkiliId == yetkiliId || od.YetkiliId == null));
                    }
                    else
                    {
                        query = query.Where(od => od.YetkiliId == yetkiliId);
                    }
                }
                else
                {
                    query = query.Where(od => od.YetkiliId == yetkiliId);
                }

                var basvurular = await query
                    .OrderBy(od => od.Basvuru.BasvuruTarihi)
                    .Take(10)
                    .ToListAsync();

                // Yetkili isimlerini güncelle
                foreach (var detay in basvurular)
                {
                    detay.YetkiliAdi = detay.Yetkili?.AdSoyad ?? "Yetkili Atanmadı";
                }

                return basvurular;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen başvurular yüklenirken hata oluştu.");
                return new List<OnayDetay>();
            }
        }

        private async Task<dynamic> GetDetayliIstatistikler(int userId, int fakulteId, int? bolumId, string? onayAsamasi)
        {
            try
            {
                var buAyBaslangic = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                // Yetkili bilgilerini al - Bu kritik önem taşıyor
                var yetkili = await _context.Yetkililer.FirstOrDefaultAsync(y => y.Id == userId);
                if (yetkili == null)
                {
                    _logger.LogWarning("Yetkili bulunamadı: {UserId}", userId);
                    return GetEmptyStats();
                }

                // Onay aşaması bilgilerini al
                var onayAsama = await _context.OnayAsamalari
                    .FirstOrDefaultAsync(oa => oa.AsamaAdi == onayAsamasi);

                // Bu ay istatistikleri - Bu kısım doğru
                var buAyOnaylanan = await _context.OnayDetaylari
                    .CountAsync(od => od.YetkiliId == userId &&
                                    od.Durum == ApplicationConstants.Onaylandi &&
                                    od.OnayTarihi >= buAyBaslangic);

                var buAyReddedilen = await _context.OnayDetaylari
                    .CountAsync(od => od.YetkiliId == userId &&
                                    od.Durum == ApplicationConstants.Reddedildi &&
                                    od.OnayTarihi >= buAyBaslangic);

                // Toplam istatistikler - Bu kısım doğru
                var toplamOnaylanan = await _context.OnayDetaylari
                    .CountAsync(od => od.YetkiliId == userId && od.Durum == ApplicationConstants.Onaylandi);

                var toplamReddedilen = await _context.OnayDetaylari
                    .CountAsync(od => od.YetkiliId == userId && od.Durum == ApplicationConstants.Reddedildi);

                // DÜZELTME: Bekleyen başvuru sayısı - Aşama türüne göre filtreleme
                int bekleyenBasvuru;
                if (onayAsama != null)
                {
                    if (onayAsama.Ortak)
                    {
                        // Ortak aşama: Sadece bu yetkili
                        bekleyenBasvuru = await _context.OnayDetaylari
                            .CountAsync(od => od.AsamaAdi == onayAsamasi &&
                                            od.Durum == ApplicationConstants.Beklemede &&
                                            od.YetkiliId == userId &&
                                            od.Basvuru.MevcutAsama == od.AsamaNo);
                    }
                    else if (onayAsama.BolumBazli && yetkili.BolumId.HasValue)
                    {
                        // Bölüm bazlı: Yetkililerin kendi bölümündeki öğrenciler
                        bekleyenBasvuru = await _context.OnayDetaylari
                            .CountAsync(od => od.AsamaAdi == onayAsamasi &&
                                            od.Durum == ApplicationConstants.Beklemede &&
                                            od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                            od.Basvuru.Ogrenci.BolumId == yetkili.BolumId.Value &&
                                            (od.YetkiliId == userId || od.YetkiliId == null) &&
                                            od.Basvuru.MevcutAsama == od.AsamaNo);
                    }
                    else if (onayAsama.FakulteBazli)
                    {
                        // Fakülte bazlı: Kendi fakültesindeki öğrenciler
                        bekleyenBasvuru = await _context.OnayDetaylari
                            .CountAsync(od => od.AsamaAdi == onayAsamasi &&
                                            od.Durum == ApplicationConstants.Beklemede &&
                                            od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                                            (od.YetkiliId == userId || od.YetkiliId == null) &&
                                            od.Basvuru.MevcutAsama == od.AsamaNo);
                    }
                    else
                    {
                        // Diğer durumlar: Sadece bu yetkili
                        bekleyenBasvuru = await _context.OnayDetaylari
                            .CountAsync(od => od.AsamaAdi == onayAsamasi &&
                                            od.Durum == ApplicationConstants.Beklemede &&
                                            od.YetkiliId == userId &&
                                            od.Basvuru.MevcutAsama == od.AsamaNo);
                    }
                }
                else
                {
                    // Aşama bilgisi yoksa: Sadece bu yetkili
                    bekleyenBasvuru = await _context.OnayDetaylari
                        .CountAsync(od => od.AsamaAdi == onayAsamasi &&
                                    od.Durum == ApplicationConstants.Beklemede &&
                                    od.YetkiliId == userId &&
                                    od.Basvuru.MevcutAsama == od.AsamaNo);
                }

                // DÜZELTME: Başvuru türü bazında istatistik - Aşama türüne göre filtreleme
                var turBazindaQuery = _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .Where(b => b.OnayDetaylari.Any(od => od.YetkiliId == userId));

                if (onayAsama != null)
                {
                    if (onayAsama.BolumBazli && yetkili.BolumId.HasValue)
                    {
                        // Bölüm bazlı: Sadece kendi bölümü
                        turBazindaQuery = turBazindaQuery.Where(b =>
                            b.Ogrenci.FakulteId == fakulteId &&
                            b.Ogrenci.BolumId == yetkili.BolumId.Value);
                    }
                    else if (onayAsama.FakulteBazli)
                    {
                        // Fakülte bazlı: Sadece kendi fakültesi
                        turBazindaQuery = turBazindaQuery.Where(b => b.Ogrenci.FakulteId == fakulteId);
                    }
                    // Ortak aşama için ek filtreleme gerekmiyor
                }

                var turBazindaIstatistik = await turBazindaQuery
                    .GroupBy(b => b.BasvuruTuru)
                    .Select(g => new
                    {
                        Tur = g.Key,
                        Adet = g.Count()
                    })
                    .ToListAsync();

                // Ortalama yanıt süresi - Bu kısım doğru
                var yanitSureleri = await _context.OnayDetaylari
                    .Where(od => od.YetkiliId == userId && od.OnayTarihi.HasValue)
                    .Select(od => EF.Functions.DateDiffDay(od.Basvuru.BasvuruTarihi, od.OnayTarihi.Value))
                    .ToListAsync();

                var ortalamaYanitSuresi = yanitSureleri.Any() ? yanitSureleri.Average() : 0;

                // DÜZELTME: Son işlemler - Aşama türüne göre filtreleme
                var sonIslemlerQuery = _context.OnayDetaylari
                    .Include(od => od.Basvuru)
                        .ThenInclude(b => b.Ogrenci)
                            .ThenInclude(o => o.Bolum)
                    .Where(od => od.YetkiliId == userId && od.OnayTarihi.HasValue);

                if (onayAsama != null)
                {
                    if (onayAsama.BolumBazli && yetkili.BolumId.HasValue)
                    {
                        // Bölüm bazlı: Sadece kendi bölümü
                        sonIslemlerQuery = sonIslemlerQuery.Where(od =>
                            od.Basvuru.Ogrenci.FakulteId == fakulteId &&
                            od.Basvuru.Ogrenci.BolumId == yetkili.BolumId.Value);
                    }
                    else if (onayAsama.FakulteBazli)
                    {
                        // Fakülte bazlı: Sadece kendi fakültesi
                        sonIslemlerQuery = sonIslemlerQuery.Where(od =>
                            od.Basvuru.Ogrenci.FakulteId == fakulteId);
                    }
                    // Ortak aşama için ek filtreleme gerekmiyor
                }

                var sonIslemler = await sonIslemlerQuery
                    .OrderByDescending(od => od.OnayTarihi)
                    .Take(10)
                    .Select(od => new
                    {
                        OgrenciAdi = od.Basvuru.Ogrenci.TamAd,
                        OgrenciNo = od.Basvuru.Ogrenci.OgrenciNo,
                        BolumAdi = od.Basvuru.Ogrenci.Bolum.BolumAdi,
                        BasvuruTuru = od.Basvuru.BasvuruTuru,
                        Durum = od.Durum,
                        IslemTarihi = od.OnayTarihi.Value
                    })
                    .ToListAsync();

                var toplamBasvuru = toplamOnaylanan + toplamReddedilen;
                var onayOrani = toplamBasvuru > 0 ? Math.Round((double)toplamOnaylanan / toplamBasvuru * 100, 1) : 0;
                var redOrani = toplamBasvuru > 0 ? Math.Round((double)toplamReddedilen / toplamBasvuru * 100, 1) : 0;

                return new
                {
                    ToplamBasvuru = toplamBasvuru,
                    BekleyenBasvuru = bekleyenBasvuru,
                    Bekleyen = bekleyenBasvuru,
                    OnaylananBasvuru = toplamOnaylanan,
                    ReddettigimBasvuru = toplamReddedilen,
                    OrtalamaSure = Math.Round(ortalamaYanitSuresi, 1),
                    OnayOrani = onayOrani,
                    RedOrani = redOrani,
                    BuAyToplam = buAyOnaylanan + buAyReddedilen,
                    BuAyOnaylanan = buAyOnaylanan,
                    BuAyReddedilen = buAyReddedilen,
                    BuAyIslenen = buAyOnaylanan + buAyReddedilen,
                    BasvuruTurleri = turBazindaIstatistik,
                    SonIslemler = sonIslemler
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler hesaplanırken hata oluştu.");
                return GetEmptyStats();
            }
        }

        private dynamic GetEmptyStats()
        {
            return new
            {
                ToplamBasvuru = 0,
                BekleyenBasvuru = 0,
                Bekleyen = 0,
                OnaylananBasvuru = 0,
                ReddettigimBasvuru = 0,
                OrtalamaSure = 0.0,
                OnayOrani = 0.0,
                RedOrani = 0.0,
                BuAyToplam = 0,
                BuAyOnaylanan = 0,
                BuAyReddedilen = 0,
                BuAyIslenen = 0,
                BasvuruTurleri = new List<object>(),
                SonIslemler = new List<object>()
            };
        }
    }
}