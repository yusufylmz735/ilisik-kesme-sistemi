using System.Net.Mail;
using System.Net;
using HayataAtilmaFormu.Models;
using Microsoft.Extensions.Options;

namespace HayataAtilmaFormu.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        public string SenderName { get; set; } = "Şırnak Üniversitesi İlişik Kesme Sistemi";
    }

    public interface IEmailService
    {
        Task SendBasvuruOnayEmailAsync(Basvuru basvuru);
        Task SendBasvuruRedEmailAsync(Basvuru basvuru, string redNedeni);
        Task SendBasvuruDurumBildirimiAsync(string toEmail, string ogrenciAdi, string ogrenciNo, string basvuruTuru, string durum);
        Task SendYetkiliOnayBildirimiAsync(string email, string adSoyad, string durum, string mesaj);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendBasvuruOnayEmailAsync(Basvuru basvuru)
        {
            try
            {
                var subject = "✅ Başvurunuz Onaylandı - İlişik Kesme Formu";

                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(45deg, #28a745, #20c997); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>🎉 Tebrikler! Başvurunuz Onaylandı</h2>
                        </div>
                        
                        <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #28a745;'>
                            <h3 style='color: #28a745; margin-top: 0;'>Sayın {basvuru.Ogrenci.TamAd},</h3>
                            
                            <p style='font-size: 16px; line-height: 1.6;'>İlişik kesme başvurunuz tüm onay aşamalarından geçerek <strong style='color: #28a745;'>başarıyla onaylanmıştır</strong>.</p>
                            
                            <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                                <h4 style='color: #1f2f51; margin-top: 0; border-bottom: 2px solid #e9ecef; padding-bottom: 10px;'>Başvuru Detayları</h4>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; width: 35%; color: #495057;'>Öğrenci No:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.OgrenciNo}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Fakülte:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.Fakulte.FakulteAdi}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Bölüm:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.Bolum.BolumAdi}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Başvuru Türü:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee;'><span style='background: #007bff; color: white; padding: 4px 8px; border-radius: 4px; font-size: 14px;'>{basvuru.BasvuruTuru}</span></td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Başvuru Tarihi:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.BasvuruTarihi:dd MMMM yyyy, HH:mm}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; font-weight: bold; color: #495057;'>Onay Tarihi:</td>
                                        <td style='padding: 12px; color: #28a745; font-weight: bold;'>{DateTime.Now:dd MMMM yyyy, HH:mm}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <div style='background: #d4edda; color: #155724; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #c3e6cb;'>
                                <h4 style='margin: 0 0 15px 0; color: #155724;'>✅ Sonraki Adımlar</h4>
                                <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                    <li><strong>Sisteme giriş yapın</strong> ve onaylı formunuzu PDF olarak indirin</li>
                                    <li><strong>Onaylı PDF'i yazdırın</strong> ve gerekli birimlere sunun</li>
                                    <li><strong>İlgili birimlerden</strong> işlemlerinizi tamamlayın</li>
                                    <li>Sorularınız için <strong>öğrenci işleri ile iletişime geçin</strong></li>
                                </ul>
                            </div>
                            
                            <div style='background: #fff3cd; color: #856404; padding: 15px; border-radius: 8px; margin: 20px 0; border: 1px solid #ffeaa7;'>
                                <p style='margin: 0; font-weight: bold;'>⚠️ Önemli: Bu onay belgesini saklamanızı ve gerekli işlemlerinizi zamanında tamamlamanızı öneririz.</p>
                            </div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                            <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                            <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi</p>
                            <p style='margin: 5px 0;'>Öğrenci İşleri Sistemi</p>
                            <p style='margin: 15px 0 5px 0; font-size: 12px; color: #adb5bd;'>© 2025 - İlişik Kesme Otomasyon Sistemi</p>
                        </div>
                    </div>";

                await SendEmailAsync(basvuru.Ogrenci.Email, subject, body);
                _logger.LogInformation("Onay e-postası başarıyla gönderildi: {Email}", basvuru.Ogrenci.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onay e-postası gönderilirken hata oluştu: {Email}", basvuru.Ogrenci.Email);
                throw;
            }
        }

        public async Task SendBasvuruRedEmailAsync(Basvuru basvuru, string redNedeni)
        {
            try
            {
                var subject = "📋 Başvuru Değerlendirme Sonucu - İlişik Kesme Formu";

                var body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <div style='background: linear-gradient(45deg, #dc3545, #c82333); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                            <h2 style='margin: 0;'>📋 Başvuru Değerlendirme Sonucu</h2>
                        </div>
                        
                        <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #dc3545;'>
                            <h3 style='color: #dc3545; margin-top: 0;'>Sayın {basvuru.Ogrenci.TamAd},</h3>
                            
                            <p style='font-size: 16px; line-height: 1.6;'>İlişik kesme başvurunuz değerlendirilmiş ve aşağıdaki sonuca ulaşılmıştır:</p>
                            
                            <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                                <h4 style='color: #1f2f51; margin-top: 0; border-bottom: 2px solid #e9ecef; padding-bottom: 10px;'>Başvuru Detayları</h4>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; width: 35%; color: #495057;'>Öğrenci No:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.OgrenciNo}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Fakülte:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.Fakulte.FakulteAdi}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Bölüm:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.Ogrenci.Bolum.BolumAdi}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Başvuru Türü:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee;'><span style='background: #007bff; color: white; padding: 4px 8px; border-radius: 4px; font-size: 14px;'>{basvuru.BasvuruTuru}</span></td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; color: #495057;'>Başvuru Tarihi:</td>
                                        <td style='padding: 12px; border-bottom: 1px solid #eee; color: #212529;'>{basvuru.BasvuruTarihi:dd MMMM yyyy, HH:mm}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; font-weight: bold; color: #495057;'>Değerlendirme Tarihi:</td>
                                        <td style='padding: 12px; color: #dc3545; font-weight: bold;'>{DateTime.Now:dd MMMM yyyy, HH:mm}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <div style='background: #f8d7da; color: #721c24; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #f5c6cb;'>
                                <h4 style='margin: 0 0 15px 0; color: #721c24;'>⚠️ Değerlendirme Sonucu</h4>
                                <p style='margin: 0; font-weight: bold; font-size: 16px;'>Başvurunuz değerlendirme aşamasında kabul edilmemiştir.</p>
                            </div>
                            
                            {(!string.IsNullOrEmpty(redNedeni) ? $@"
                                <div style='background: #fff3cd; color: #856404; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #ffeaa7;'>
                                    <h4 style='margin: 0 0 15px 0; color: #856404;'>📝 Açıklama</h4>
                                    <p style='margin: 0; font-size: 15px; line-height: 1.6;'><em>{redNedeni}</em></p>
                                </div>
                            " : "")}
                            
                            <div style='background: #d1ecf1; color: #0c5460; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #bee5eb;'>
                                <h4 style='margin: 0 0 15px 0; color: #0c5460;'>🔄 Sonraki Adımlar</h4>
                                <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                    <li><strong>Gerekli düzenlemeleri</strong> yaptıktan sonra yeni başvuru yapabilirsiniz</li>
                                    <li><strong>Başvuru koşullarını</strong> tekrar gözden geçiriniz</li>
                                    <li><strong>Eksik belgelerinizi</strong> tamamlayın</li>
                                    <li>Sorularınız için <strong>öğrenci işleri ile iletişime geçin</strong></li>
                                    <li><strong>Danışman öğretim üyenizden</strong> rehberlik alabilirsiniz</li>
                                </ul>
                            </div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                            <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                            <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi</p>
                            <p style='margin: 5px 0;'>Öğrenci İşleri Sistemi</p>
                            <p style='margin: 15px 0 5px 0; font-size: 12px; color: #adb5bd;'>© 2025 - İlişik Kesme Otomasyon Sistemi</p>
                        </div>
                    </div>";

                await SendEmailAsync(basvuru.Ogrenci.Email, subject, body);
                _logger.LogInformation("Red e-postası başarıyla gönderildi: {Email}", basvuru.Ogrenci.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Red e-postası gönderilirken hata oluştu: {Email}", basvuru.Ogrenci.Email);
                throw;
            }
        }

        public async Task SendBasvuruDurumBildirimiAsync(string toEmail, string ogrenciAdi, string ogrenciNo, string basvuruTuru, string durum)
        {
            try
            {
                var subject = durum == "Onaylandi" ?
                    "✅ Başvurunuz Onaylandı - İlişik Kesme Formu" :
                    "📋 Başvuru Değerlendirme Sonucu";

                var body = durum == "Onaylandi" ?
                    CreateOnayEmailBody(ogrenciAdi, ogrenciNo, basvuruTuru) :
                    CreateRedEmailBody(ogrenciAdi, ogrenciNo, basvuruTuru);

                await SendEmailAsync(toEmail, subject, body);
                _logger.LogInformation("Durum bildirimi e-postası başarıyla gönderildi: {Email}, Durum: {Durum}", toEmail, durum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durum bildirimi e-postası gönderilirken hata oluştu: {Email}", toEmail);
                throw;
            }
        }

        public async Task SendYetkiliOnayBildirimiAsync(string email, string adSoyad, string durum, string mesaj)
        {
            try
            {
                var subject = durum == "Onaylandı" || durum == "Onayla" ?
                    "✅ Yetkili Başvurunuz Onaylandı - İlişik Kesme Sistemi" :
                    "📋 Yetkili Başvuru Değerlendirme Sonucu";

                var body = durum == "Onaylandı" || durum == "Onayla" ?
                    CreateYetkiliOnayEmailBody(adSoyad, mesaj) :
                    CreateYetkiliRedEmailBody(adSoyad, mesaj);

                await SendEmailAsync(email, subject, body);
                _logger.LogInformation("Yetkili onay bildirimi e-postası gönderildi: {Email}, Durum: {Durum}", email, durum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili onay bildirimi e-postası gönderilirken hata oluştu: {Email}", email);
                throw;
            }
        }

        private string CreateOnayEmailBody(string ogrenciAdi, string ogrenciNo, string basvuruTuru)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(45deg, #28a745, #20c997); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                        <h2 style='margin: 0;'>🎉 Tebrikler! Başvurunuz Onaylandı</h2>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #28a745;'>
                        <h3 style='color: #28a745; margin-top: 0;'>Sayın {ogrenciAdi},</h3>
                        
                        <p style='font-size: 16px; line-height: 1.6;'>İlişik kesme başvurunuz <strong style='color: #28a745;'>başarıyla onaylanmıştır</strong>.</p>
                        
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <table style='width: 100%; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; width: 35%;'>Öğrenci No:</td>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee;'>{ogrenciNo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold;'>Başvuru Türü:</td>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee;'>{basvuruTuru}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; font-weight: bold;'>Onay Tarihi:</td>
                                    <td style='padding: 12px; color: #28a745; font-weight: bold;'>{DateTime.Now:dd MMMM yyyy, HH:mm}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <div style='background: #d4edda; color: #155724; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <h4 style='margin: 0 0 15px 0;'>✅ Sonraki Adımlar</h4>
                            <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                <li>Sisteme giriş yaparak onaylı formunuzu PDF olarak indirin</li>
                                <li>Gerekli birimlere onaylı belgenizi sunun</li>
                                <li>Sorularınız için öğrenci işleri ile iletişime geçin</li>
                            </ul>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                        <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                        <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi - Öğrenci İşleri Sistemi</p>
                    </div>
                </div>";
        }

        private string CreateRedEmailBody(string ogrenciAdi, string ogrenciNo, string basvuruTuru)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(45deg, #dc3545, #c82333); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                        <h2 style='margin: 0;'>📋 Başvuru Değerlendirme Sonucu</h2>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #dc3545;'>
                        <h3 style='color: #dc3545; margin-top: 0;'>Sayın {ogrenciAdi},</h3>
                        
                        <p style='font-size: 16px; line-height: 1.6;'>İlişik kesme başvurunuz değerlendirilmiş ve kabul edilmemiştir.</p>
                        
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <table style='width: 100%; border-collapse: collapse;'>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold; width: 35%;'>Öğrenci No:</td>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee;'>{ogrenciNo}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee; font-weight: bold;'>Başvuru Türü:</td>
                                    <td style='padding: 12px; border-bottom: 1px solid #eee;'>{basvuruTuru}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; font-weight: bold;'>Değerlendirme Tarihi:</td>
                                    <td style='padding: 12px; color: #dc3545; font-weight: bold;'>{DateTime.Now:dd MMMM yyyy, HH:mm}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <div style='background: #d1ecf1; color: #0c5460; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                            <h4 style='margin: 0 0 15px 0;'>🔄 Sonraki Adımlar</h4>
                            <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                <li>Gerekli düzenlemeleri yaptıktan sonra yeni başvuru yapabilirsiniz</li>
                                <li>Sorularınız için öğrenci işleri ile iletişime geçin</li>
                            </ul>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                        <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                        <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi - Öğrenci İşleri Sistemi</p>
                    </div>
                </div>";
        }

        private string CreateYetkiliOnayEmailBody(string yetkiliAdi, string mesaj)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(45deg, #28a745, #20c997); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                        <h2 style='margin: 0;'>🎉 Yetkili Başvurunuz Onaylandı!</h2>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #28a745;'>
                        <h3 style='color: #28a745; margin-top: 0;'>Sayın {yetkiliAdi},</h3>
                        
                        <p style='font-size: 16px; line-height: 1.6;'>Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz <strong style='color: #28a745;'>başarıyla onaylanmıştır</strong>.</p>
                        
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                            <h4 style='color: #1f2f51; margin-top: 0; border-bottom: 2px solid #e9ecef; padding-bottom: 10px;'>Onay Detayları</h4>
                            <p style='color: #212529; line-height: 1.6;'>{mesaj}</p>
                            <p style='color: #28a745; font-weight: bold; margin-top: 15px;'>Onay Tarihi: {DateTime.Now:dd MMMM yyyy, HH:mm}</p>
                        </div>
                        
                        <div style='background: #d4edda; color: #155724; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #c3e6cb;'>
                            <h4 style='margin: 0 0 15px 0; color: #155724;'>✅ Sonraki Adımlar</h4>
                            <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                <li><strong>Sisteme giriş yapın</strong> ve görevlerinizi yerine getirin</li>
                                <li><strong>Size atanan başvuruları</strong> değerlendirin</li>
                                <li><strong>İlgili dökümanları</strong> inceleyin</li>
                                <li>Sorularınız için <strong>sistem yöneticisi ile iletişime geçin</strong></li>
                            </ul>
                        </div>
                        
                        <div style='text-align: center; padding: 20px; background: #e7f3ff; border-radius: 8px; margin-top: 20px;'>
                            <p style='margin: 0; font-weight: bold; color: #0066cc;'>Sisteme giriş yapmak için aşağıdaki bağlantıyı kullanın:</p>
                            <p style='margin: 10px 0 0 0;'><a href='https://yourwebsite.com/Account/Login' style='color: #0066cc; text-decoration: none; font-weight: bold;'>Sisteme Giriş Yap</a></p>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                        <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                        <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi</p>
                        <p style='margin: 5px 0;'>İlişik Kesme Otomasyon Sistemi</p>
                        <p style='margin: 15px 0 5px 0; font-size: 12px; color: #adb5bd;'>© 2025 - Yetkili Yönetim Sistemi</p>
                    </div>
                </div>";
        }

        private string CreateYetkiliRedEmailBody(string yetkiliAdi, string mesaj)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(45deg, #dc3545, #c82333); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; text-align: center;'>
                        <h2 style='margin: 0;'>📋 Yetkili Başvuru Değerlendirme Sonucu</h2>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #dc3545;'>
                        <h3 style='color: #dc3545; margin-top: 0;'>Sayın {yetkiliAdi},</h3>
                        
                        <p style='font-size: 16px; line-height: 1.6;'>Şırnak Üniversitesi İlişik Kesme Sistemi'ne yetkili başvurunuz değerlendirilmiş ve maalesef kabul edilmemiştir.</p>
                        
                        <div style='background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                            <h4 style='color: #1f2f51; margin-top: 0; border-bottom: 2px solid #e9ecef; padding-bottom: 10px;'>Değerlendirme Detayları</h4>
                            <p style='color: #212529; line-height: 1.6;'>{mesaj}</p>
                            <p style='color: #dc3545; font-weight: bold; margin-top: 15px;'>Değerlendirme Tarihi: {DateTime.Now:dd MMMM yyyy, HH:mm}</p>
                        </div>
                        
                        <div style='background: #d1ecf1; color: #0c5460; padding: 20px; border-radius: 8px; margin: 20px 0; border: 1px solid #bee5eb;'>
                            <h4 style='margin: 0 0 15px 0; color: #0c5460;'>🔄 Sonraki Adımlar</h4>
                            <ul style='margin: 0; padding-left: 20px; line-height: 1.8;'>
                                <li><strong>Gerekli düzenlemeleri</strong> yaptıktan sonra yeni başvuru yapabilirsiniz</li>
                                <li><strong>Başvuru koşullarını</strong> tekrar gözden geçiriniz</li>
                                <li><strong>Eksik belgelerinizi</strong> tamamlayın</li>
                                <li>Sorularınız için <strong>sistem yöneticisi ile iletişime geçin</strong></li>
                            </ul>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 40px; padding-top: 20px; border-top: 2px solid #e9ecef; color: #6c757d; font-size: 14px;'>
                        <p style='margin: 5px 0;'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                        <p style='margin: 5px 0; font-weight: bold; color: #1f2f51;'>Şırnak Üniversitesi</p>
                        <p style='margin: 5px 0;'>İlişik Kesme Otomasyon Sistemi</p>
                        <p style='margin: 15px 0 5px 0; font-size: 12px; color: #adb5bd;'>© 2025 - Yetkili Yönetim Sistemi</p>
                    </div>
                </div>";
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrEmpty(_emailSettings.SmtpServer) || string.IsNullOrEmpty(_emailSettings.FromEmail))
                {
                    _logger.LogWarning("E-posta ayarları eksik. SMTP Server: {SmtpServer}, FromEmail: {FromEmail}",
                        _emailSettings.SmtpServer, _emailSettings.FromEmail);
                    return;
                }

                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
                {
                    EnableSsl = _emailSettings.EnableSsl,
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password)
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("E-posta başarıyla gönderildi: {Email}, Konu: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderimi başarısız: {Email}, Hata: {Error}", toEmail, ex.Message);
                throw;
            }
        }
    }
}