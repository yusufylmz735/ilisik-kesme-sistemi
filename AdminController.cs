using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HayataAtilmaFormu.Services; // E-posta servisi için eklendi

namespace HayataAtilmaFormu.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IEmailService _emailService; // E-posta servisi eklendi

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService; // Dependency injection
        }

        // GET: Admin/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var stats = new
                {
                    BekleyenYetkililer = await _context.Yetkililer.CountAsync(y => y.OnayBekliyor),
                    AktifYetkililer = await _context.Yetkililer.CountAsync(y => !y.OnayBekliyor && y.Aktif),
                    ToplamOgrenciler = await _context.Ogrenciler.CountAsync(),
                    AktifBasvurular = await _context.Basvurular.CountAsync(b => b.Durum == "Aktif")
                };
                ViewBag.Stats = stats;

                var bekleyenYetkililer = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .Include(y => y.Bolum)
                    .Where(y => y.OnayBekliyor)
                    .OrderBy(y => y.KayitTarihi)
                    .Take(10)
                    .ToListAsync();

                ViewBag.BekleyenYetkililer = bekleyenYetkililer;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin index yüklenirken hata oluştu.");
                ViewBag.Error = "Hata oluştu: " + ex.Message;
                ViewBag.BekleyenYetkililer = new List<Yetkili>();
                return View();
            }
        }

        // GET: Admin/YetkiliYonetimi
        public async Task<IActionResult> YetkiliYonetimi()
        {
            try
            {
                var yetkililer = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .Include(y => y.Bolum)
                    .ToListAsync();

                ViewBag.Fakulteler = await _context.Fakulteler.ToListAsync();
                ViewBag.OnayAsamalari = new SelectList(await GetOnayAsamalariList(), "Value", "Text");

                return View(yetkililer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili listesi yüklenirken hata oluştu.");
                ViewBag.Error = "Yetkili listesi yüklenirken hata oluştu: " + ex.Message;
                return View(new List<Yetkili>());
            }
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

                _logger.LogInformation($"Fakülte {fakulteId} için {bolumler.Count} bölüm bulundu.");
                return Json(bolumler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bölüm yükleme hatası.");
                return Json(new { success = false, message = "Bölümler yüklenirken hata oluştu." });
            }
        }

        // POST: Admin/YetkiliEkle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliEkle(Yetkili yetkili)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (await _context.Yetkililer.AnyAsync(y => y.Email == yetkili.Email))
                    {
                        return Json(new { success = false, message = "Bu e-posta adresi zaten kayıtlı!" });
                    }

                    yetkili.KayitTarihi = DateTime.Now;
                    yetkili.OnayBekliyor = true;
                    yetkili.Aktif = false;

                    _context.Yetkililer.Add(yetkili);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Yetkili başarıyla eklendi, onay bekliyor." });
                }

                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = "Geçersiz veri: " + string.Join("; ", errors) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili eklenirken hata oluştu.");
                return Json(new { success = false, message = "Yetkili eklenirken hata oluştu: " + ex.Message });
            }
        }

        // POST: Admin/YetkiliSil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliSil(int yetkiliId)
        {
            try
            {
                var yetkili = await _context.Yetkililer.FindAsync(yetkiliId);
                if (yetkili == null)
                {
                    return Json(new { success = false, message = "Yetkili bulunamadı!" });
                }

                _context.Yetkililer.Remove(yetkili);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Yetkili başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili silinirken hata oluştu.");
                return Json(new { success = false, message = "Yetkili silinirken hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopluYetkiliOnayla(List<int> yetkiliIds, string karar, string aciklama = "")
        {
            try
            {
                if (yetkiliIds == null || !yetkiliIds.Any())
                {
                    return Json(new { success = false, message = "İşlem yapılacak yetkili seçilmedi." });
                }
                if (karar != "Onayla" && karar != "Reddet")
                {
                    return Json(new { success = false, message = "Geçersiz işlem türü." });
                }
                if (karar == "Reddet" && string.IsNullOrWhiteSpace(aciklama))
                {
                    return Json(new { success = false, message = "Red işlemi için açıklama gereklidir." });
                }

                var yetkililer = await _context.Yetkililer
                    .Where(y => yetkiliIds.Contains(y.Id) && y.OnayBekliyor)
                    .ToListAsync();

                if (!yetkililer.Any())
                {
                    return Json(new { success = false, message = "İşlem yapılacak onay bekleyen yetkili bulunamadı." });
                }

                int basariliSayisi = 0;
                var basariliYetkililer = new List<Yetkili>();

                foreach (var yetkili in yetkililer)
                {
                    if (karar == "Onayla")
                    {
                        yetkili.OnayBekliyor = false;
                        yetkili.Aktif = true;
                        yetkili.RedNedeni = null;
                        yetkili.OnayTarihi = DateTime.Now;
                    }
                    else
                    {
                        yetkili.OnayBekliyor = false;
                        yetkili.Aktif = false;
                        yetkili.RedNedeni = aciklama;
                        yetkili.OnayTarihi = DateTime.Now;
                    }
                    basariliSayisi++;
                    basariliYetkililer.Add(yetkili);
                }

                await _context.SaveChangesAsync();

                // TOPLU E-POSTA BİLDİRİMİ GÖNDER
                int emailBasarili = 0;
                int emailHatali = 0;

                foreach (var yetkili in basariliYetkililer)
                {
                    try
                    {
                        if (karar == "Onayla")
                        {
                            await _emailService.SendYetkiliOnayBildirimiAsync(
                                yetkili.Email,
                                yetkili.AdSoyad,
                                "Onaylandı",
                                $"Merhaba {yetkili.AdSoyad},\n\n" +
                                $"Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz onaylanmıştır.\n\n" +
                                $"Artık sisteme giriş yaparak görevlerinizi yerine getirebilirsiniz.\n\n" +
                                $"Onay Tarihi: {yetkili.OnayTarihi:dd.MM.yyyy HH:mm}\n" +
                                $"Pozisyon: {yetkili.OnayAsamasi}\n\n" +
                                $"Giriş için: https://yourwebsite.com/Account/Login\n\n" +
                                $"İyi çalışmalar dileriz."
                            );
                        }
                        else
                        {
                            await _emailService.SendYetkiliOnayBildirimiAsync(
                                yetkili.Email,
                                yetkili.AdSoyad,
                                "Reddedildi",
                                $"Merhaba {yetkili.AdSoyad},\n\n" +
                                $"Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz maalesef reddedilmiştir.\n\n" +
                                $"Red Nedeni: {aciklama}\n" +
                                $"Red Tarihi: {yetkili.OnayTarihi:dd.MM.yyyy HH:mm}\n\n" +
                                $"Sorularınız için iletişime geçebilirsiniz.\n\n" +
                                $"İyi günler dileriz."
                            );
                        }
                        emailBasarili++;
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Toplu e-posta gönderimi hatası - Yetkili: {Email}", yetkili.Email);
                        emailHatali++;
                    }
                }

                var mesaj = karar == "Onayla"
                    ? $"{basariliSayisi} yetkili onaylandı. E-posta bildirimi: {emailBasarili} başarılı, {emailHatali} hatalı."
                    : $"{basariliSayisi} yetkili reddedildi. E-posta bildirimi: {emailBasarili} başarılı, {emailHatali} hatalı.";

                return Json(new { success = true, message = mesaj });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu yetkili onaylama işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu: " + ex.Message });
            }
        }

        // POST: Admin/YetkiliDurumDegistir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliDurumDegistir(int yetkiliId, bool aktif)
        {
            try
            {
                var yetkili = await _context.Yetkililer.FindAsync(yetkiliId);
                if (yetkili == null)
                {
                    return Json(new { success = false, message = "Yetkili bulunamadı!" });
                }

                var eskiDurum = yetkili.Aktif;
                yetkili.Aktif = aktif;
                _context.Yetkililer.Update(yetkili);
                await _context.SaveChangesAsync();

                // E-POSTA BİLDİRİMİ GÖNDER
                try
                {
                    if (eskiDurum != aktif) // Sadece durum değiştiyse e-posta gönder
                    {
                        var durum = aktif ? "Aktif" : "Pasif";
                        var mesaj = aktif ?
                            $"Merhaba {yetkili.AdSoyad},\n\nHesabınız tekrar aktif hale getirilmiştir.\n\nSisteme giriş yaparak görevlerinizi yerine getirebilirsiniz.\n\nDurum Değişiklik Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}\n\nGiriş için: https://yourwebsite.com/Account/Login\n\nİyi çalışmalar dileriz." :
                            $"Merhaba {yetkili.AdSoyad},\n\nHesabınız geçici olarak pasif hale getirilmiştir.\n\nBu durumla ilgili sorularınız için lütfen sistem yöneticisi ile iletişime geçin.\n\nDurum Değişiklik Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}\n\nİyi günler dileriz.";

                        await _emailService.SendYetkiliOnayBildirimiAsync(
                            yetkili.Email,
                            yetkili.AdSoyad,
                            durum,
                            mesaj
                        );

                        _logger.LogInformation("Yetkili durum değişikliği e-posta bildirimi gönderildi: {YetkiliId} - {Email} - {Durum}",
                            yetkiliId, yetkili.Email, durum);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Yetkili durum değişikliği e-posta bildirimi gönderilirken hata oluştu: {YetkiliId}", yetkiliId);
                    // E-posta hatası ana işlemi durdurmaz
                }

                return Json(new { success = true, message = $"Yetkili {(aktif ? "aktif" : "pasif")} yapıldı ve e-posta bildirimi gönderildi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili durum değiştirilirken hata oluştu.");
                return Json(new { success = false, message = "Durum değiştirilirken hata oluştu: " + ex.Message });
            }
        }

        // GET: Admin/YetkiliDetay
        public async Task<IActionResult> YetkiliDetay(int id)
        {
            try
            {
                var yetkili = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .Include(y => y.Bolum)
                    .FirstOrDefaultAsync(y => y.Id == id);

                if (yetkili == null)
                {
                    ViewBag.Error = "Yetkili bulunamadı!";
                    return View();
                }

                return View(yetkili);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili detayları yüklenirken hata oluştu.");
                ViewBag.Error = "Yetkili detayları yüklenirken hata oluştu: " + ex.Message;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliOnayla(int yetkiliId, string karar, string aciklama)
        {
            try
            {
                if (yetkiliId <= 0)
                {
                    return Json(new { success = false, message = "Geçersiz yetkili ID!" });
                }

                if (string.IsNullOrEmpty(karar) || (karar != "Onayla" && karar != "Reddet"))
                {
                    return Json(new { success = false, message = "Geçersiz karar!" });
                }

                var yetkili = await _context.Yetkililer.FindAsync(yetkiliId);
                if (yetkili == null)
                {
                    return Json(new { success = false, message = "Yetkili bulunamadı!" });
                }

                if (!yetkili.OnayBekliyor)
                {
                    return Json(new { success = false, message = "Bu yetkili zaten işlenmiş!" });
                }

                if (karar == "Onayla")
                {
                    yetkili.OnayBekliyor = false;
                    yetkili.Aktif = true;
                    yetkili.RedNedeni = null;
                    yetkili.OnayTarihi = DateTime.Now;
                }
                else if (karar == "Reddet")
                {
                    if (string.IsNullOrWhiteSpace(aciklama))
                    {
                        return Json(new { success = false, message = "Red nedeni belirtmelisiniz!" });
                    }

                    yetkili.OnayBekliyor = false;
                    yetkili.Aktif = false;
                    yetkili.RedNedeni = aciklama.Trim();
                    yetkili.OnayTarihi = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // E-POSTA BİLDİRİMİ GÖNDER
                try
                {
                    if (karar == "Onayla")
                    {
                        // Onay e-postası gönder
                        await _emailService.SendYetkiliOnayBildirimiAsync(
                            yetkili.Email,
                            yetkili.AdSoyad,
                            "Onaylandı",
                            $"Merhaba {yetkili.AdSoyad},\n\n" +
                            $"Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz onaylanmıştır.\n\n" +
                            $"Artık sisteme giriş yaparak görevlerinizi yerine getirebilirsiniz.\n\n" +
                            $"Onay Tarihi: {yetkili.OnayTarihi:dd.MM.yyyy HH:mm}\n" +
                            $"Pozisyon: {yetkili.OnayAsamasi}\n\n" +
                            $"Giriş için: https://yourwebsite.com/Account/Login\n\n" +
                            $"İyi çalışmalar dileriz."
                        );
                    }
                    else
                    {
                        // Red e-postası gönder
                        await _emailService.SendYetkiliOnayBildirimiAsync(
                            yetkili.Email,
                            yetkili.AdSoyad,
                            "Reddedildi",
                            $"Merhaba {yetkili.AdSoyad},\n\n" +
                            $"Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz maalesef reddedilmiştir.\n\n" +
                            $"Red Nedeni: {aciklama}\n" +
                            $"Red Tarihi: {yetkili.OnayTarihi:dd.MM.yyyy HH:mm}\n\n" +
                            $"Sorularınız için iletişime geçebilirsiniz.\n\n" +
                            $"İyi günler dileriz."
                        );
                    }

                    _logger.LogInformation("Yetkili onay e-posta bildirimi gönderildi: {YetkiliId} - {Email} - {Durum}",
                        yetkiliId, yetkili.Email, karar);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Yetkili e-posta bildirimi gönderilirken hata oluştu: {YetkiliId}", yetkiliId);
                    // E-posta hatası ana işlemi durdurmaz
                }

                return Json(new
                {
                    success = true,
                    message = karar == "Onayla"
                        ? "Yetkili başarıyla onaylandı ve e-posta bildirimi gönderildi!"
                        : "Yetkili başvurusu reddedildi ve e-posta bildirimi gönderildi!",
                    redirectUrl = Url.Action("YetkiliBasvurulari", "Admin")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili onaylama işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OgrenciSil(int id, bool hardDelete = false)
        {
            try
            {
                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Basvurular)
                        .ThenInclude(b => b.OnayDetaylari) // Başvuru detaylarını da dahil et
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ogrenci == null)
                {
                    return Json(new { success = false, message = "Öğrenci bulunamadı." });
                }

                if (hardDelete)
                {
                    // Önce ilişkili kayıtları sil
                    if (ogrenci.Basvurular?.Any() == true)
                    {
                        foreach (var basvuru in ogrenci.Basvurular.ToList())
                        {
                            // Başvuru onay detaylarını sil
                            if (basvuru.OnayDetaylari?.Any() == true)
                            {
                                _context.OnayDetaylari.RemoveRange(basvuru.OnayDetaylari);
                            }
                            // Başvuruyu sil
                            _context.Basvurular.Remove(basvuru);
                        }
                    }

                    // Son olarak öğrenciyi sil
                    _context.Ogrenciler.Remove(ogrenci);
                    await _context.SaveChangesAsync();

                    _logger.LogWarning("Öğrenci ve tüm ilişkili kayıtları kalıcı olarak silindi: {OgrenciId} - {OgrenciAdi}", id, ogrenci.TamAd);
                    return Json(new { success = true, message = "Öğrenci ve tüm başvuruları kalıcı olarak silindi." });
                }
                else
                {
                    // Soft delete
                    ogrenci.Aktif = false;
                    ogrenci.GuncellenmeTarihi = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Öğrenci pasif yapıldı: {OgrenciId} - {OgrenciAdi}", id, ogrenci.TamAd);
                    return Json(new { success = true, message = "Öğrenci pasif yapıldı." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci silme işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OgrenciGeriYukle(int id)
        {
            try
            {
                var ogrenci = await _context.Ogrenciler.FirstOrDefaultAsync(o => o.Id == id);

                if (ogrenci == null)
                {
                    return Json(new { success = false, message = "Öğrenci bulunamadı." });
                }

                // Soft delete'ten geri yükleme
                ogrenci.Aktif = true;
                ogrenci.GuncellenmeTarihi = DateTime.Now;

                // Email ve OgrenciNo'dan _DELETED_ kısmını temizle
                if (ogrenci.Email.Contains("_DELETED_"))
                {
                    ogrenci.Email = ogrenci.Email.Split("_DELETED_")[0];
                }
                if (ogrenci.OgrenciNo.Contains("_DEL_"))
                {
                    ogrenci.OgrenciNo = ogrenci.OgrenciNo.Split("_DEL_")[0];
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Öğrenci geri yüklendi: {OgrenciId} - {OgrenciAdi}", id, ogrenci.TamAd);
                return Json(new { success = true, message = "Öğrenci başarıyla geri yüklendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci geri yükleme işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
            }
        }

        // Pasif öğrencileri listeleme
        public async Task<IActionResult> PasifOgrenciler()
        {
            try
            {
                var pasifOgrenciler = await _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .Where(o => !o.Aktif)
                    .OrderByDescending(o => o.GuncellenmeTarihi)
                    .ToListAsync();

                return View(pasifOgrenciler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pasif öğrenciler yüklenirken hata oluştu.");
                ViewBag.Error = "Veriler yüklenirken hata oluştu.";
                return View(new List<Ogrenci>());
            }
        }

        // Toplu öğrenci silme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopluOgrenciSil(List<int> ogrenciIds, bool hardDelete = false)
        {
            try
            {
                if (ogrenciIds == null || !ogrenciIds.Any())
                {
                    return Json(new { success = false, message = "Silinecek öğrenci seçilmedi." });
                }

                int basariliSayisi = 0;
                int hataliSayisi = 0;
                var hataMesajlari = new List<string>();

                foreach (var id in ogrenciIds)
                {
                    var ogrenci = await _context.Ogrenciler
                        .Include(o => o.Basvurular)
                            .ThenInclude(b => b.OnayDetaylari)
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (ogrenci == null)
                    {
                        hataliSayisi++;
                        hataMesajlari.Add($"ID {id}: Öğrenci bulunamadı");
                        continue;
                    }

                    try
                    {
                        if (hardDelete)
                        {
                            // İlişkili kayıtları cascade delete ile sil
                            if (ogrenci.Basvurular?.Any() == true)
                            {
                                foreach (var basvuru in ogrenci.Basvurular.ToList())
                                {
                                    // Başvuru onay detaylarını sil
                                    if (basvuru.OnayDetaylari?.Any() == true)
                                    {
                                        _context.OnayDetaylari.RemoveRange(basvuru.OnayDetaylari);
                                    }
                                    // Başvuruyu sil
                                    _context.Basvurular.Remove(basvuru);
                                }
                            }

                            // Öğrenciyi sil
                            _context.Ogrenciler.Remove(ogrenci);
                            _logger.LogWarning("Toplu silme: Öğrenci ve tüm ilişkili kayıtları silindi: {OgrenciId} - {OgrenciAdi}", id, ogrenci.TamAd);
                        }
                        else
                        {
                            // Soft delete
                            ogrenci.Aktif = false;
                            ogrenci.GuncellenmeTarihi = DateTime.Now;
                        }
                        basariliSayisi++;
                    }
                    catch (Exception ex)
                    {
                        hataliSayisi++;
                        hataMesajlari.Add($"{ogrenci.TamAd}: {ex.Message}");
                        _logger.LogError(ex, "Toplu silme hatası: {OgrenciAdi}", ogrenci.TamAd);
                    }
                }

                await _context.SaveChangesAsync();

                var mesaj = hardDelete
                    ? $"{basariliSayisi} öğrenci kalıcı olarak silindi."
                    : $"{basariliSayisi} öğrenci pasif yapıldı.";

                if (hataliSayisi > 0)
                {
                    mesaj += $" {hataliSayisi} öğrenci işlenemedi.";
                }

                return Json(new
                {
                    success = true,
                    message = mesaj,
                    details = hataMesajlari
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu öğrenci silme işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "Toplu işlem sırasında hata oluştu: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        // GET: Admin/OgrenciDetay
        public async Task<IActionResult> OgrenciDetay(int id)
        {
            try
            {
                var ogrenci = await _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .Include(o => o.UserProfile)
                    .Include(o => o.Basvurular)
                        .ThenInclude(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                            .ThenInclude(od => od.Yetkili)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ogrenci == null)
                {
                    ViewBag.Error = "Öğrenci bulunamadı.";
                    return RedirectToAction("Ogrenciler");
                }

                // Yetkili adlarını güncelle
                if (ogrenci.Basvurular != null)
                {
                    foreach (var basvuru in ogrenci.Basvurular)
                    {
                        foreach (var detay in basvuru.OnayDetaylari)
                        {
                            detay.YetkiliAdi = detay.Yetkili?.AdSoyad ?? "Yetkili Atanmadı";
                        }
                    }
                }

                return View(ogrenci);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci detayı yüklenirken hata oluştu.");
                ViewBag.Error = "Öğrenci detayı yüklenirken hata oluştu.";
                return RedirectToAction("Ogrenciler");
            }
        }

        // GET: Admin/Ogrenciler - Öğrenci listesi
        public async Task<IActionResult> Ogrenciler(string durum = "Tumu", int sayfa = 1, int sayfaBoyutu = 20, string arama = "")
        {
            try
            {
                var query = _context.Ogrenciler
                    .Include(o => o.Fakulte)
                    .Include(o => o.Bolum)
                    .AsQueryable();

                // Durum filtreleme
                if (durum == "Aktif")
                {
                    query = query.Where(o => o.Aktif);
                }
                else if (durum == "Pasif")
                {
                    query = query.Where(o => !o.Aktif);
                }

                // Arama filtreleme
                if (!string.IsNullOrWhiteSpace(arama))
                {
                    query = query.Where(o =>
                        o.Ad.Contains(arama) ||
                        o.Soyad.Contains(arama) ||
                        o.OgrenciNo.Contains(arama) ||
                        o.Email.Contains(arama));
                }

                var toplamKayit = await query.CountAsync();
                var ogrenciler = await query
                    .OrderByDescending(o => o.KayitTarihi)
                    .Skip((sayfa - 1) * sayfaBoyutu)
                    .Take(sayfaBoyutu)
                    .ToListAsync();

                ViewBag.Durum = durum;
                ViewBag.Arama = arama;
                ViewBag.MevcutSayfa = sayfa;
                ViewBag.ToplamSayfa = (int)Math.Ceiling((double)toplamKayit / sayfaBoyutu);
                ViewBag.ToplamKayit = toplamKayit;

                return View(ogrenciler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci listesi yüklenirken hata oluştu.");
                ViewBag.Error = "Öğrenci listesi yüklenirken hata oluştu.";
                return View(new List<Ogrenci>());
            }
        }
        public async Task<IActionResult> Basvurular(string durum = "Tumu", int sayfa = 1, int sayfaBoyutu = 20, string arama = "")
        {
            try
            {
                var query = _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .AsQueryable();

                // Durum filtreleme
                if (durum != "Tumu")
                {
                    query = query.Where(b => b.Durum == durum);
                }

                // Arama filtreleme
                if (!string.IsNullOrWhiteSpace(arama))
                {
                    query = query.Where(b =>
                        b.Ogrenci.Ad.Contains(arama) ||
                        b.Ogrenci.Soyad.Contains(arama) ||
                        b.Ogrenci.OgrenciNo.Contains(arama) ||
                        b.BasvuruTuru.Contains(arama));
                }

                var toplamKayit = await query.CountAsync();
                var basvurular = await query
                    .OrderByDescending(b => b.BasvuruTarihi)
                    .Skip((sayfa - 1) * sayfaBoyutu)
                    .Take(sayfaBoyutu)
                    .ToListAsync();

                ViewBag.Durum = durum;
                ViewBag.Arama = arama;
                ViewBag.MevcutSayfa = sayfa;
                ViewBag.ToplamSayfa = (int)Math.Ceiling((double)toplamKayit / sayfaBoyutu);
                ViewBag.ToplamKayit = toplamKayit;

                // Durum sayıları için istatistikler
                ViewBag.Stats = new
                {
                    Tumu = await _context.Basvurular.CountAsync(),
                    Beklemede = await _context.Basvurular.CountAsync(b => b.Durum == "Beklemede"),
                    Onaylandi = await _context.Basvurular.CountAsync(b => b.Durum == "Onaylandi"),
                    Reddedildi = await _context.Basvurular.CountAsync(b => b.Durum == "Reddedildi")
                };

                return View(basvurular);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru listesi yüklenirken hata oluştu.");
                ViewBag.Error = "Başvuru listesi yüklenirken hata oluştu.";
                return View(new List<Basvuru>());
            }
        }

        // GET: Admin/BasvuruDetay
        public async Task<IActionResult> BasvuruDetay(int id)
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Include(b => b.OnayDetaylari.OrderBy(od => od.AsamaNo))
                        .ThenInclude(od => od.Yetkili)
                    .Include(b => b.OnayDetaylari)
                        .ThenInclude(od => od.BeklenenFakulte)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (basvuru == null)
                {
                    ViewBag.Error = "Başvuru bulunamadı.";
                    return RedirectToAction("Basvurular");
                }

                // Onay aşamalarını getir
                var onayAsamalari = await _context.OnayAsamalari
                    .Where(oa => oa.OgrenciTuru == basvuru.Ogrenci.OgrenciTuru && oa.Aktif)
                    .OrderBy(oa => oa.SiraNo)
                    .ToListAsync();

                ViewBag.OnayAsamalari = onayAsamalari;

                // Mevcut aşamada yetkili atama için yetkilileri getir
                if (basvuru.MevcutAsama <= onayAsamalari.Count)
                {
                    var mevcutAsama = onayAsamalari[basvuru.MevcutAsama - 1];
                    var yetkililer = await GetYetkilileriByAsama(mevcutAsama, basvuru.Ogrenci.FakulteId, basvuru.Ogrenci.BolumId);
                    ViewBag.MevcutAsamaYetkililer = yetkililer;
                }

                return View(basvuru);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru detayı yüklenirken hata oluştu.");
                ViewBag.Error = "Başvuru detayı yüklenirken hata oluştu.";
                return RedirectToAction("Basvurular");
            }
        }

        // POST: Admin/BasvuruDurumGuncelle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BasvuruDurumGuncelle(int basvuruId, string yeniDurum, string aciklama = "")
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                var eskiDurum = basvuru.Durum;
                basvuru.Durum = yeniDurum;
                basvuru.SonGuncellemeTarihi = DateTime.Now;

                if (yeniDurum == "Onaylandi" || yeniDurum == "Reddedildi")
                {
                    basvuru.TamamlanmaTarihi = DateTime.Now;
                    if (yeniDurum == "Reddedildi" && !string.IsNullOrEmpty(aciklama))
                    {
                        basvuru.RedNedeni = aciklama;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Başvuru durumu güncellendi: {BasvuruId} - {EskiDurum} -> {YeniDurum}",
                    basvuruId, eskiDurum, yeniDurum);

                return Json(new { success = true, message = "Başvuru durumu başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru durumu güncellenirken hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsamaGeriAl(int basvuruId, int asamaNo)
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                // Belirtilen aşama ve sonrasındaki onay detaylarını sil
                var silinecekDetaylar = basvuru.OnayDetaylari
                    .Where(od => od.AsamaNo >= asamaNo)
                    .ToList();

                if (silinecekDetaylar.Any())
                {
                    _context.OnayDetaylari.RemoveRange(silinecekDetaylar);
                }

                // Başvuru durumunu güncelle
                basvuru.MevcutAsama = asamaNo;
                basvuru.Durum = ApplicationConstants.Beklemede;
                basvuru.SonGuncellemeTarihi = DateTime.Now;
                basvuru.TamamlanmaTarihi = null;
                basvuru.RedNedeni = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Aşama geri alındı: {BasvuruId} - Aşama {AsamaNo}", basvuruId, asamaNo);
                return Json(new { success = true, message = $"Aşama {asamaNo} ve sonrası geri alındı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aşama geri alma işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
            }
        }

        // POST: Admin/AsamaOnayla
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsamaOnayla(int basvuruId, int asamaNo, int yetkiliId, string karar, string aciklama = "")
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .Include(b => b.Ogrenci)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                var yetkili = await _context.Yetkililer.FindAsync(yetkiliId);
                if (yetkili == null)
                {
                    return Json(new { success = false, message = "Yetkili bulunamadı." });
                }

                // Mevcut aşama detayını bul veya oluştur
                var onayDetay = basvuru.OnayDetaylari.FirstOrDefault(od => od.AsamaNo == asamaNo);
                if (onayDetay == null)
                {
                    // Onay aşaması bilgisini al
                    var onayAsamasi = await _context.OnayAsamalari
                        .FirstOrDefaultAsync(oa => oa.SiraNo == asamaNo &&
                                                 oa.OgrenciTuru == basvuru.Ogrenci.OgrenciTuru &&
                                                 oa.Aktif);

                    if (onayAsamasi == null)
                    {
                        return Json(new { success = false, message = "Geçersiz onay aşaması." });
                    }

                    onayDetay = new OnayDetay
                    {
                        BasvuruId = basvuruId,
                        AsamaNo = asamaNo,
                        AsamaAdi = onayAsamasi.AsamaAdi,
                        YetkiliId = yetkiliId,
                        YetkiliAdi = yetkili.AdSoyad,
                        YetkiliPozisyonu = yetkili.OnayAsamasi,
                        BeklenenFakulteId = basvuru.Ogrenci.FakulteId,
                        Durum = ApplicationConstants.Beklemede
                    };
                    _context.OnayDetaylari.Add(onayDetay);
                }
                else
                {
                    onayDetay.YetkiliId = yetkiliId;
                    onayDetay.YetkiliAdi = yetkili.AdSoyad;
                }

                // Kararı uygula
                if (karar == "Onayla")
                {
                    onayDetay.Durum = ApplicationConstants.Onaylandi;
                    onayDetay.OnayTarihi = DateTime.Now;
                    onayDetay.Aciklama = aciklama;

                    // Sonraki aşamaya geç
                    if (asamaNo >= basvuru.ToplamAsama)
                    {
                        // Son aşama tamamlandı
                        basvuru.Durum = ApplicationConstants.Onaylandi;
                        basvuru.TamamlanmaTarihi = DateTime.Now;
                        basvuru.MevcutAsama = basvuru.ToplamAsama;
                    }
                    else
                    {
                        basvuru.MevcutAsama = asamaNo + 1;
                    }
                }
                else if (karar == "Reddet")
                {
                    if (string.IsNullOrWhiteSpace(aciklama))
                    {
                        return Json(new { success = false, message = "Red nedeni belirtmelisiniz." });
                    }

                    onayDetay.Durum = ApplicationConstants.Reddedildi;
                    onayDetay.OnayTarihi = DateTime.Now;
                    onayDetay.Aciklama = aciklama;

                    // Başvuruyu reddet
                    basvuru.Durum = ApplicationConstants.Reddedildi;
                    basvuru.RedNedeni = aciklama;
                    basvuru.TamamlanmaTarihi = DateTime.Now;
                }

                basvuru.SonGuncellemeTarihi = DateTime.Now;
                basvuru.SonIslemYapanYetkiliId = yetkiliId;

                await _context.SaveChangesAsync();

                var mesaj = karar == "Onayla" ? "Aşama başarıyla onaylandı." : "Aşama reddedildi.";
                return Json(new { success = true, message = mesaj });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aşama onaylama işlemi sırasında hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında hata oluştu." });
            }
        }

        // POST: Admin/BasvuruSil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BasvuruSil(int basvuruId)
        {
            try
            {
                var basvuru = await _context.Basvurular
                    .Include(b => b.OnayDetaylari)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                // İlişkili onay detaylarını sil
                if (basvuru.OnayDetaylari.Any())
                {
                    _context.OnayDetaylari.RemoveRange(basvuru.OnayDetaylari);
                }

                // Başvuruyu sil
                _context.Basvurular.Remove(basvuru);
                await _context.SaveChangesAsync();

                _logger.LogWarning("Başvuru silindi: {BasvuruId} - {OgrenciAdi}", basvuruId, basvuru.Ogrenci?.TamAd);
                return Json(new { success = true, message = "Başvuru başarıyla silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru silinirken hata oluştu.");
                return Json(new { success = false, message = "Başvuru silinirken hata oluştu." });
            }
        }

        // POST: Admin/BasvuruDuzenle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BasvuruDuzenle(int basvuruId, string basvuruTuru, string aciklama, string oncelikDurumu)
        {
            try
            {
                _logger.LogInformation("Başvuru düzenleme işlemi başlatıldı - ID: {BasvuruId}", basvuruId);

                var basvuru = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                    .FirstOrDefaultAsync(b => b.Id == basvuruId);

                if (basvuru == null)
                {
                    _logger.LogWarning("Başvuru bulunamadı - ID: {BasvuruId}", basvuruId);
                    return Json(new { success = false, message = "Başvuru bulunamadı." });
                }

                // Değişiklikleri logla
                _logger.LogInformation("Eski değerler - Tür: {EskiTur}, Açıklama: {EskiAciklama}, Öncelik: {EskiOncelik}",
                    basvuru.BasvuruTuru, basvuru.Aciklama, basvuru.OncelikDurumu);

                // Güncelleme işlemi
                var eskiTur = basvuru.BasvuruTuru;
                var eskiAciklama = basvuru.Aciklama;
                var eskiOncelik = basvuru.OncelikDurumu;

                basvuru.BasvuruTuru = basvuruTuru?.Trim();
                basvuru.Aciklama = aciklama?.Trim();
                basvuru.OncelikDurumu = oncelikDurumu?.Trim();
                basvuru.SonGuncellemeTarihi = DateTime.Now;

                // Değişiklik kontrolü
                var degisiklikVar = eskiTur != basvuru.BasvuruTuru ||
                                   eskiAciklama != basvuru.Aciklama ||
                                   eskiOncelik != basvuru.OncelikDurumu;

                if (!degisiklikVar)
                {
                    return Json(new { success = false, message = "Herhangi bir değişiklik yapılmadı." });
                }

                // Veritabanına kaydet
                _context.Basvurular.Update(basvuru);
                var affectedRows = await _context.SaveChangesAsync();

                _logger.LogInformation("Başvuru güncellendi - ID: {BasvuruId}, Etkilenen satır: {AffectedRows}", basvuruId, affectedRows);

                // Değişiklik detayları
                var degisiklikMesaji = new List<string>();
                if (eskiTur != basvuru.BasvuruTuru)
                    degisiklikMesaji.Add($"Başvuru Türü: '{eskiTur}' → '{basvuru.BasvuruTuru}'");
                if (eskiAciklama != basvuru.Aciklama)
                    degisiklikMesaji.Add($"Açıklama güncellendi");
                if (eskiOncelik != basvuru.OncelikDurumu)
                    degisiklikMesaji.Add($"Öncelik: '{eskiOncelik}' → '{basvuru.OncelikDurumu}'");

                var detayMesaj = string.Join(", ", degisiklikMesaji);

                // Öğrenciye e-posta bildirimi gönder (isteğe bağlı)
                try
                {
                    if (basvuru.Ogrenci != null && !string.IsNullOrEmpty(basvuru.Ogrenci.Email))
                    {
                        await _emailService.SendBasvuruDurumBildirimiAsync(
                            basvuru.Ogrenci.Email,
                            basvuru.Ogrenci.TamAd,
                            basvuru.Ogrenci.OgrenciNo,
                            basvuru.BasvuruTuru,
                            "Düzenlendi"
                        );
                        _logger.LogInformation("Başvuru düzenleme bildirimi gönderildi: {OgrenciEmail}", basvuru.Ogrenci.Email);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Başvuru düzenleme e-posta bildirimi gönderilemedi");
                }

                return Json(new
                {
                    success = true,
                    message = $"Başvuru başarıyla güncellendi. Değişiklikler: {detayMesaj}",
                    degisiklikDetay = detayMesaj,
                    guncellemeTarihi = basvuru.SonGuncellemeTarihi?.ToString("dd.MM.yyyy HH:mm")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Başvuru düzenlenirken hata oluştu - ID: {BasvuruId}", basvuruId);
                return Json(new { success = false, message = "Başvuru düzenlenirken hata oluştu: " + ex.Message });
            }
        }

        // Yardımcı method: Aşamaya göre yetkilileri getir
        private async Task<List<Yetkili>> GetYetkilileriByAsama(OnayAsama asama, int fakulteId, int bolumId)
        {
            var query = _context.Yetkililer
                .Include(y => y.Fakulte)
                .Include(y => y.Bolum)
                .Where(y => y.Aktif && !y.OnayBekliyor);

            if (asama.Ortak)
            {
                // Ortak aşamalar için tüm uygun yetkililer
                query = query.Where(y => y.OnayAsamasi == asama.YetkiliPozisyonu);
            }
            else if (asama.BolumBazli)
            {
                // Bölüm bazlı aşamalar
                query = query.Where(y => y.BolumId == bolumId &&
                                        y.OnayAsamasi == asama.YetkiliPozisyonu);
            }
            else if (asama.FakulteBazli)
            {
                // Fakülte bazlı aşamalar
                query = query.Where(y => y.FakulteId == fakulteId &&
                                        y.OnayAsamasi == asama.YetkiliPozisyonu);
            }

            return await query.OrderBy(y => y.AdSoyad).ToListAsync();
        }

        // GET: Admin/SistemBilgileri
        public IActionResult SistemBilgileri()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sistem bilgileri yüklenirken hata oluştu.");
                ViewBag.Error = "Sistem bilgileri yüklenirken hata oluştu: " + ex.Message;
                return View();
            }
        }

        // GET: Admin/Istatistikler
        public IActionResult Istatistikler()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler yüklenirken hata oluştu.");
                ViewBag.Error = "İstatistikler yüklenirken hata oluştu: " + ex.Message;
                return View();
            }
        }
        public async Task<IActionResult> YetkiliBasvurulari(string durum = "Tumu", int sayfa = 1, int sayfaBoyutu = 20, string arama = "")
        {
            try
            {
                var query = _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .Include(y => y.Bolum)
                    .AsQueryable();

                // Durum filtreleme
                if (durum == "Beklemede")
                {
                    query = query.Where(y => y.OnayBekliyor);
                }
                else if (durum == "Onaylandi")
                {
                    query = query.Where(y => !y.OnayBekliyor && y.Aktif);
                }
                else if (durum == "Reddedildi")
                {
                    query = query.Where(y => !y.OnayBekliyor && !y.Aktif && !string.IsNullOrEmpty(y.RedNedeni));
                }

                // Arama filtreleme
                if (!string.IsNullOrWhiteSpace(arama))
                {
                    query = query.Where(y =>
                        y.AdSoyad.Contains(arama) ||
                        y.Email.Contains(arama) ||
                        y.OnayAsamasi.Contains(arama));
                }

                var toplamKayit = await query.CountAsync();
                var yetkililer = await query
                    .OrderByDescending(y => y.KayitTarihi)
                    .Skip((sayfa - 1) * sayfaBoyutu)
                    .Take(sayfaBoyutu)
                    .ToListAsync();

                ViewBag.Durum = durum;
                ViewBag.Arama = arama;
                ViewBag.MevcutSayfa = sayfa;
                ViewBag.ToplamSayfa = (int)Math.Ceiling((double)toplamKayit / sayfaBoyutu);
                ViewBag.ToplamKayit = toplamKayit;

                // İstatistikler
                ViewBag.Stats = new
                {
                    Tumu = await _context.Yetkililer.CountAsync(),
                    Beklemede = await _context.Yetkililer.CountAsync(y => y.OnayBekliyor),
                    Onaylandi = await _context.Yetkililer.CountAsync(y => !y.OnayBekliyor && y.Aktif),
                    Reddedildi = await _context.Yetkililer.CountAsync(y => !y.OnayBekliyor && !y.Aktif && !string.IsNullOrEmpty(y.RedNedeni))
                };

                return View(yetkililer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili başvuruları listesi yüklenirken hata oluştu.");
                ViewBag.Error = "Yetkili başvuruları listesi yüklenirken hata oluştu.";
                return View(new List<Yetkili>());
            }
        }

        // GET: Admin/MasterLogin
        public IActionResult MasterLogin()
        {
            try
            {
                var today = DateTime.Now;
                ViewBag.DailyPasswordHint = $"Format: SIRNAK_{today:yyyyMMdd} (Bugün: SIRNAK_{today:yyyyMMdd})";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Master giriş sayfası yüklenirken hata oluştu.");
                ViewBag.Error = "Master giriş sayfası yüklenirken hata oluştu: " + ex.Message;
                return View();
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

        // POST: Admin/MasterLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MasterLogin(string masterKey, string masterPassword)
        {
            try
            {
                var today = DateTime.Now;
                var expectedPassword = $"SIRNAK_{today:yyyyMMdd}";

                var expectedMasterKey = "SuperSecretKey123"; // Gerçekte appsettings.json'dan çek

                if (masterKey == expectedMasterKey && masterPassword == expectedPassword)
                {
                    ViewBag.Success = "Master giriş başarılı!";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.Error = "Geçersiz master key veya şifre!";
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Master giriş sırasında hata oluştu.");
                ViewBag.Error = "Master giriş sırasında hata oluştu: " + ex.Message;
                return View();
            }
        }

        // GET: Admin/GetAdminStats
        [HttpGet]
        public async Task<IActionResult> GetAdminStats(string dateFilter)
        {
            try
            {
                // Tarih filtresi
                DateTime? startDate = null;
                if (!string.IsNullOrEmpty(dateFilter))
                {
                    switch (dateFilter)
                    {
                        case "last7days":
                            startDate = DateTime.Now.AddDays(-7);
                            break;
                        case "last30days":
                            startDate = DateTime.Now.AddDays(-30);
                            break;
                    }
                }

                // Temel sorgu
                var basvuruQuery = _context.Basvurular.AsQueryable();
                var ogrenciQuery = _context.Ogrenciler.AsQueryable();
                var yetkiliQuery = _context.Yetkililer.AsQueryable();

                // Tarih filtresi uygula
                if (startDate.HasValue)
                {
                    basvuruQuery = basvuruQuery.Where(b => b.BasvuruTarihi >= startDate.Value);
                    ogrenciQuery = ogrenciQuery.Where(o => o.KayitTarihi >= startDate.Value);
                    yetkiliQuery = yetkiliQuery.Where(y => y.KayitTarihi >= startDate.Value);
                }

                // İstatistikleri hesapla
                var toplamYetkililer = await yetkiliQuery.CountAsync();
                var toplamOgrenciler = await ogrenciQuery.CountAsync();
                var toplamBasvurular = await basvuruQuery.CountAsync();

                var onaylananBasvurular = await basvuruQuery
                    .CountAsync(b => b.Durum == "Onaylandi" || b.Durum == "Onaylandı");

                var onayOrani = toplamBasvurular > 0 ? Math.Round((onaylananBasvurular * 100.0 / toplamBasvurular), 2) : 0;

                // Başvuru dağılımı - undefined değerleri kontrol et
                var basvuruDagilimi = await basvuruQuery
                    .GroupBy(b => b.BasvuruTuru ?? "Belirtilmemiş") // null olanları "Belirtilmemiş" yap
                    .Select(g => new {
                        Tur = g.Key,
                        Adet = g.Count()
                    })
                    .OrderByDescending(x => x.Adet)
                    .ToListAsync();

                // Durum dağılımı - undefined değerleri kontrol et
                var durumDagilimi = await basvuruQuery
                    .GroupBy(b => b.Durum ?? "Bilinmiyor") // null olanları "Bilinmiyor" yap
                    .Select(g => new {
                        Durum = g.Key,
                        Adet = g.Count()
                    })
                    .OrderByDescending(x => x.Adet)
                    .ToListAsync();

                // Fakülte bazlı dağılım - null kontrolü ekle
                var fakulteDagilimi = await ogrenciQuery
                    .Include(o => o.Fakulte)
                    .GroupBy(o => o.Fakulte != null ? o.Fakulte.FakulteAdi : "Belirtilmemiş")
                    .Select(g => new {
                        Fakulte = g.Key,
                        Adet = g.Count()
                    })
                    .OrderByDescending(x => x.Adet)
                    .Take(10)
                    .ToListAsync();

                // Aylık başvuru trendi - null kontrolü ekle
                var altıAyOnce = DateTime.Now.AddMonths(-6);
                var aylikTrendRaw = await _context.Basvurular
                    .Where(b => b.BasvuruTarihi >= altıAyOnce)
                    .GroupBy(b => new {
                        Yil = b.BasvuruTarihi.Year,
                        Ay = b.BasvuruTarihi.Month
                    })
                    .Select(g => new {
                        Yil = g.Key.Yil,
                        Ay = g.Key.Ay,
                        Adet = g.Count()
                    })
                    .OrderBy(x => x.Yil).ThenBy(x => x.Ay)
                    .ToListAsync();

                // Aylik trend'i format et - null kontrolü ekle
                var aylikTrend = aylikTrendRaw.Select(x => new {
                    Ay = $"{x.Yil:D4}-{x.Ay:D2}", // Yıl formatını düzelt
                    Adet = x.Adet
                }).ToList();

                var stats = new
                {
                    toplamYetkililer = toplamYetkililer,
                    aktifYetkililer = await yetkiliQuery.CountAsync(y => y.Aktif && !y.OnayBekliyor),
                    bekleyenYetkililer = await yetkiliQuery.CountAsync(y => y.OnayBekliyor),

                    toplamOgrenciler = toplamOgrenciler,
                    aktifOgrenciler = await ogrenciQuery.CountAsync(o => o.Aktif),

                    toplamBasvurular = toplamBasvurular,
                    beklemedekiBasvurular = await basvuruQuery.CountAsync(b => b.Durum == "Beklemede"),
                    onaylananBasvurular = onaylananBasvurular,
                    reddedilenBasvurular = await basvuruQuery.CountAsync(b => b.Durum == "Reddedildi"),

                    onayOrani = onayOrani,

                    // Dağılım verileri - artık null değer içermez
                    basvuruDagilimi = basvuruDagilimi,
                    durumDagilimi = durumDagilimi,
                    fakulteDagilimi = fakulteDagilimi,
                    aylikTrend = aylikTrend,

                    // Hızlı istatistikler
                    bugunBasvuru = await _context.Basvurular.CountAsync(b => b.BasvuruTarihi.Date == DateTime.Today),
                    buHaftaBasvuru = await _context.Basvurular.CountAsync(b => b.BasvuruTarihi >= DateTime.Now.AddDays(-7)),
                    buAyBasvuru = await _context.Basvurular.CountAsync(b => b.BasvuruTarihi >= DateTime.Now.AddDays(-30)),

                    success = true
                };

                _logger.LogInformation("İstatistikler başarıyla yüklendi - Başvuru: {ToplamBasvuru}, Öğrenci: {ToplamOgrenci}",
                    toplamBasvurular, toplamOgrenciler);

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler yüklenirken hata oluştu: {Message}", ex.Message);
                return Json(new
                {
                    success = false,
                    message = "İstatistikler yüklenirken hata oluştu: " + ex.Message,
                    toplamYetkililer = 0,
                    aktifYetkililer = 0,
                    bekleyenYetkililer = 0,
                    toplamOgrenciler = 0,
                    aktifOgrenciler = 0,
                    toplamBasvurular = 0,
                    beklemedekiBasvurular = 0,
                    onaylananBasvurular = 0,
                    reddedilenBasvurular = 0,
                    onayOrani = 0,
                    basvuruDagilimi = new List<object>(),
                    durumDagilimi = new List<object>(),
                    fakulteDagilimi = new List<object>(),
                    aylikTrend = new List<object>(),
                    bugunBasvuru = 0,
                    buHaftaBasvuru = 0,
                    buAyBasvuru = 0
                });
            }
        }
    }
}