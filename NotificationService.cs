using HayataAtilmaFormu.Data;
using HayataAtilmaFormu.Models;
using Microsoft.EntityFrameworkCore;

namespace HayataAtilmaFormu.Services
{
    public interface INotificationService
    {
        // Öğrenci bildirimleri (Email)
        Task SendOgrenciBasvuruOnayEmailAsync(Basvuru basvuru);
        Task SendOgrenciBasvuruRedEmailAsync(Basvuru basvuru, string redNedeni);

        // Yetkili bildirimleri (SMS)
        Task SendYetkiliOnayBildirimiSmsAsync(int yetkiliId, string durum, string? aciklama = null);
        Task SendYetkiliBasvuruAtamaSmsAsync(int yetkiliId, Basvuru basvuru);

        // Toplu bildirimler
        Task SendBulkYetkiliOnayBildirimiAsync(List<int> yetkiliIds, string durum);
        Task SendYetkiliOnayBildirimiEmailAsync(int yetkiliId, string durum, string aciklama);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IEmailService emailService,
            ISmsService smsService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _smsService = smsService;
            _logger = logger;
        }

        #region Öğrenci Email Bildirimleri

        public async Task SendOgrenciBasvuruOnayEmailAsync(Basvuru basvuru)
        {
            try
            {
                if (basvuru?.Ogrenci == null)
                {
                    var fullBasvuru = await _context.Basvurular
                        .Include(b => b.Ogrenci)
                            .ThenInclude(o => o.Fakulte)
                        .Include(b => b.Ogrenci)
                            .ThenInclude(o => o.Bolum)
                        .FirstOrDefaultAsync(b => b.Id == basvuru!.Id);

                    if (fullBasvuru == null)
                    {
                        _logger.LogWarning("Başvuru bulunamadı: {BasvuruId}", basvuru?.Id);
                        return;
                    }
                    basvuru = fullBasvuru;
                }

                await _emailService.SendBasvuruOnayEmailAsync(basvuru);

                // Bildirim log kaydet
                await LogNotificationAsync("EMAIL", "OGRENCI_ONAY", basvuru.Ogrenci.Email,
                    $"Başvuru onayı - {basvuru.BasvuruTuru}", true);

                _logger.LogInformation("Öğrenci onay email bildirimi gönderildi: {OgrenciEmail}", basvuru.Ogrenci.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci onay email bildirimi gönderilirken hata oluştu");

                await LogNotificationAsync("EMAIL", "OGRENCI_ONAY", basvuru?.Ogrenci?.Email ?? "unknown",
                    $"Başvuru onayı - {basvuru?.BasvuruTuru ?? "unknown"}", false, ex.Message);
            }
        }

        public async Task SendOgrenciBasvuruRedEmailAsync(Basvuru basvuru, string redNedeni)
        {
            try
            {
                if (basvuru?.Ogrenci == null)
                {
                    var fullBasvuru = await _context.Basvurular
                        .Include(b => b.Ogrenci)
                            .ThenInclude(o => o.Fakulte)
                        .Include(b => b.Ogrenci)
                            .ThenInclude(o => o.Bolum)
                        .FirstOrDefaultAsync(b => b.Id == basvuru!.Id);

                    if (fullBasvuru == null)
                    {
                        _logger.LogWarning("Başvuru bulunamadı: {BasvuruId}", basvuru?.Id);
                        return;
                    }
                    basvuru = fullBasvuru;
                }

                await _emailService.SendBasvuruRedEmailAsync(basvuru, redNedeni);

                // Bildirim log kaydet
                await LogNotificationAsync("EMAIL", "OGRENCI_RED", basvuru.Ogrenci.Email,
                    $"Başvuru reddi - {basvuru.BasvuruTuru}", true);

                _logger.LogInformation("Öğrenci red email bildirimi gönderildi: {OgrenciEmail}", basvuru.Ogrenci.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Öğrenci red email bildirimi gönderilirken hata oluştu");

                await LogNotificationAsync("EMAIL", "OGRENCI_RED", basvuru?.Ogrenci?.Email ?? "unknown",
                    $"Başvuru reddi - {basvuru?.BasvuruTuru ?? "unknown"}", false, ex.Message);
            }
        }

        #endregion

        #region Yetkili SMS Bildirimleri

        public async Task SendYetkiliOnayBildirimiSmsAsync(int yetkiliId, string durum, string? aciklama = null)
        {
            try
            {
                var yetkili = await _context.Yetkililer
                    .Include(y => y.Fakulte)
                    .FirstOrDefaultAsync(y => y.Id == yetkiliId);

                if (yetkili == null)
                {
                    _logger.LogWarning("Yetkili bulunamadı: {YetkiliId}", yetkiliId);
                    return;
                }

                if (string.IsNullOrEmpty(yetkili.Telefon))
                {
                    _logger.LogWarning("Yetkili telefon numarası bulunamadı: {YetkiliId} - {YetkiliAdi}", yetkiliId, yetkili.AdSoyad);
                    return;
                }

                var smsResult = await _smsService.SendYetkiliOnayBildirimiAsync(
                    yetkili.Telefon, yetkili.AdSoyad, durum, aciklama ?? string.Empty);

                // Bildirim log kaydet
                await LogNotificationAsync("SMS", "YETKILI_ONAY", yetkili.Telefon,
                    $"Yetkili {durum.ToLower()} - {yetkili.AdSoyad}", smsResult,
                    smsResult ? null : "SMS gönderilemedi");

                if (smsResult)
                {
                    _logger.LogInformation("Yetkili onay SMS bildirimi gönderildi: {YetkiliAdi} - {Telefon}",
                        yetkili.AdSoyad, yetkili.Telefon);
                }
                else
                {
                    _logger.LogWarning("Yetkili onay SMS bildirimi gönderilemedi: {YetkiliAdi} - {Telefon}",
                        yetkili.AdSoyad, yetkili.Telefon);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili onay SMS bildirimi gönderilirken hata oluştu: {YetkiliId}", yetkiliId);

                await LogNotificationAsync("SMS", "YETKILI_ONAY", "unknown",
                    $"Yetkili {durum.ToLower()}", false, ex.Message);
            }
        }

        public async Task SendYetkiliBasvuruAtamaSmsAsync(int yetkiliId, Basvuru basvuru)
        {
            try
            {
                var yetkili = await _context.Yetkililer
                    .FirstOrDefaultAsync(y => y.Id == yetkiliId);

                if (yetkili == null || string.IsNullOrEmpty(yetkili.Telefon))
                {
                    _logger.LogWarning("Yetkili bulunamadı veya telefon numarası yok: {YetkiliId}", yetkiliId);
                    return;
                }

                if (basvuru?.Ogrenci == null)
                {
                    var fullBasvuru = await _context.Basvurular
                        .Include(b => b.Ogrenci)
                        .FirstOrDefaultAsync(b => b.Id == basvuru!.Id);

                    if (fullBasvuru == null)
                    {
                        _logger.LogWarning("Başvuru bulunamadı: {BasvuruId}", basvuru?.Id);
                        return;
                    }
                    basvuru = fullBasvuru;
                }

                var message = $"Sayın {yetkili.AdSoyad}, yeni başvuru ataması: {basvuru.Ogrenci.TamAd} - {basvuru.BasvuruTuru}. Sisteme giriş yaparak değerlendiriniz. - Şırnak Üniversitesi";

                // SMS gönder
                var smsResult = await _smsService.SendSmsAsync(yetkili.Telefon, message);

                // Log the notification attempt
                _logger.LogInformation("Yetkili başvuru atama bildirimi: {YetkiliAdi} - {Message}", yetkili.AdSoyad, message);

                // Bildirim log kaydet
                await LogNotificationAsync("SMS", "YETKILI_ATAMA", yetkili.Telefon,
                    $"Başvuru ataması - {basvuru.Ogrenci.TamAd}", smsResult);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili başvuru atama SMS bildirimi gönderilirken hata oluştu: {YetkiliId}", yetkiliId);

                await LogNotificationAsync("SMS", "YETKILI_ATAMA", "unknown",
                    "Başvuru ataması", false, ex.Message);
            }
        }

        #endregion

        #region Toplu Bildirimler

        public async Task SendBulkYetkiliOnayBildirimiAsync(List<int> yetkiliIds, string durum)
        {
            try
            {
                var tasks = new List<Task>();
                var basariliSayisi = 0;
                var basarisizSayisi = 0;

                foreach (var yetkiliId in yetkiliIds)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await SendYetkiliOnayBildirimiSmsAsync(yetkiliId, durum);
                            Interlocked.Increment(ref basariliSayisi);
                        }
                        catch
                        {
                            Interlocked.Increment(ref basarisizSayisi);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Toplu yetkili bildirim tamamlandı. Başarılı: {Basarili}, Başarısız: {Basarisiz}",
                    basariliSayisi, basarisizSayisi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu yetkili bildirim gönderilirken hata oluştu");
            }
        }

        #endregion

        #region Helper Methods

        private async Task LogNotificationAsync(string channel, string type, string recipient,
            string content, bool success, string? errorMessage = null)
        {
            try
            {
                var log = new NotificationLog
                {
                    Channel = channel,
                    Type = type,
                    Recipient = recipient,
                    Content = content,
                    Success = success,
                    ErrorMessage = errorMessage ?? string.Empty,
                    SentAt = DateTime.Now
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bildirim log kaydı sırasında hata oluştu");
            }
        }

        #endregion

        #region Advanced Notification Methods

        public async Task SendBatchOgrenciEmailNotificationsAsync(List<int> basvuruIds, string durumType)
        {
            try
            {
                var basvurular = await _context.Basvurular
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Fakulte)
                    .Include(b => b.Ogrenci)
                        .ThenInclude(o => o.Bolum)
                    .Where(b => basvuruIds.Contains(b.Id))
                    .ToListAsync();

                var tasks = new List<Task>();

                foreach (var basvuru in basvurular)
                {
                    if (durumType == "ONAY")
                    {
                        tasks.Add(SendOgrenciBasvuruOnayEmailAsync(basvuru));
                    }
                    else if (durumType == "RED")
                    {
                        tasks.Add(SendOgrenciBasvuruRedEmailAsync(basvuru, basvuru.RedNedeni ?? string.Empty));
                    }

                    // Rate limiting için kısa bekleme
                    await Task.Delay(100);
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Toplu öğrenci email bildirimi tamamlandı: {Count} başvuru", basvurular.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu öğrenci email bildirimi gönderilirken hata oluştu");
            }
        }

        public async Task<List<NotificationLog>> GetNotificationHistoryAsync(string? channel = null,
            DateTime? startDate = null, DateTime? endDate = null, int pageSize = 50, int page = 1)
        {
            try
            {
                var query = _context.NotificationLogs.AsQueryable();

                if (!string.IsNullOrEmpty(channel))
                    query = query.Where(n => n.Channel == channel);

                if (startDate.HasValue)
                    query = query.Where(n => n.SentAt >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(n => n.SentAt <= endDate.Value);

                return await query
                    .OrderByDescending(n => n.SentAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bildirim geçmişi alınırken hata oluştu");
                return new List<NotificationLog>();
            }
        }

        public async Task<NotificationStats> GetNotificationStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.NotificationLogs.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(n => n.SentAt >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(n => n.SentAt <= endDate.Value);

                var stats = await query.GroupBy(n => new { n.Channel, n.Success })
                    .Select(g => new { g.Key.Channel, g.Key.Success, Count = g.Count() })
                    .ToListAsync();

                return new NotificationStats
                {
                    TotalSent = stats.Sum(s => s.Count),
                    EmailSent = stats.Where(s => s.Channel == "EMAIL").Sum(s => s.Count),
                    SmsSent = stats.Where(s => s.Channel == "SMS").Sum(s => s.Count),
                    SuccessfulSent = stats.Where(s => s.Success).Sum(s => s.Count),
                    FailedSent = stats.Where(s => !s.Success).Sum(s => s.Count),
                    EmailSuccessRate = CalculateSuccessRate(stats, "EMAIL"),
                    SmsSuccessRate = CalculateSuccessRate(stats, "SMS")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bildirim istatistikleri alınırken hata oluştu");
                return new NotificationStats();
            }
        }

        private static double CalculateSuccessRate(IEnumerable<dynamic> stats, string channel)
        {
            var channelStats = stats.Where(s => s.Channel == channel);
            var total = channelStats.Sum(s => (int)s.Count);
            var successful = channelStats.Where(s => s.Success).Sum(s => (int)s.Count);

            return total > 0 ? (double)successful / total * 100 : 0;
        }

        public Task SendYetkiliOnayBildirimiEmailAsync(int yetkiliId, string durum, string aciklama)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    // Notification Stats Model
    public class NotificationStats
    {
        public int TotalSent { get; set; }
        public int EmailSent { get; set; }
        public int SmsSent { get; set; }
        public int SuccessfulSent { get; set; }
        public int FailedSent { get; set; }
        public double EmailSuccessRate { get; set; }
        public double SmsSuccessRate { get; set; }
    }
}