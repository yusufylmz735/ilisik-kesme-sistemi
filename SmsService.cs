using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HayataAtilmaFormu.Services
{
    public class SmsSettings
    {
        public string ApiUrl { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Originator { get; set; } = "SIRNAK UNIV";
        public string Provider { get; set; } = "netgsm"; // netgsm, iletimerkezi, twilio
        public bool EnableSms { get; set; } = true;
    }

    public interface ISmsService
    {
        Task<bool> SendPasswordResetSmsAsync(string phoneNumber, string newPassword, string userType);
        Task<bool> SendYetkiliOnayBildirimiAsync(string phoneNumber, string yetkiliAdi, string durum, string aciklama = "");
        Task<string> GenerateRandomPassword();
        Task<bool> SendSmsAsync(string telefon, string message); // Public interface method
    }

    public class SmsService : ISmsService
    {
        private readonly SmsSettings _smsSettings;
        private readonly ILogger<SmsService> _logger;
        private readonly HttpClient _httpClient;

        public SmsService(IOptions<SmsSettings> smsSettings, ILogger<SmsService> logger, HttpClient httpClient)
        {
            _smsSettings = smsSettings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<bool> SendPasswordResetSmsAsync(string phoneNumber, string newPassword, string userType)
        {
            try
            {
                var message = $"Şırnak Üniversitesi - Yeni şifreniz: {newPassword} - Bu şifreyi ilk girişinizde değiştirmeyi unutmayın.";

                return await SendSmsAsync(phoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama SMS'i gönderilirken hata oluştu: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> SendYetkiliOnayBildirimiAsync(string phoneNumber, string yetkiliAdi, string durum, string aciklama = "")
        {
            try
            {
                var message = durum == "Onaylandi" || durum == "Onayla"
                    ? $"Sayın {yetkiliAdi}, yetkili başvurunuz ONAYLANDI. Sisteme giriş yapabilirsiniz. - Şırnak Üniversitesi"
                    : $"Sayın {yetkiliAdi}, yetkili başvurunuz REDDEDİLDİ. {(!string.IsNullOrEmpty(aciklama) ? "Detay: " + aciklama : "")} - Şırnak Üniversitesi";

                return await SendSmsAsync(phoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetkili onay bildirimi SMS'i gönderilirken hata oluştu: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<string> GenerateRandomPassword()
        {
            await Task.Delay(1); // Async method olması için minimal delay eklendi

            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var random = new Random();
            var result = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }

        // PUBLIC method - Interface'i implement eder
        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                _logger.LogInformation("SMS gönderim başlatılıyor - Telefon: {PhoneNumber}, EnableSms: {EnableSms}", phoneNumber, _smsSettings.EnableSms);

                if (!_smsSettings.EnableSms)
                {
                    _logger.LogInformation("SMS servisi devre dışı. Mesaj gönderilmedi: {PhoneNumber}", phoneNumber);
                    return true; // Test ortamında true dön
                }

                if (string.IsNullOrEmpty(_smsSettings.ApiUrl) || string.IsNullOrEmpty(_smsSettings.Username))
                {
                    _logger.LogError("SMS ayarları eksik - ApiUrl: {ApiUrl}, Username: {Username}, Provider: {Provider}",
                        _smsSettings.ApiUrl, _smsSettings.Username, _smsSettings.Provider);
                    return false;
                }

                // Telefon numarasını formatla
                phoneNumber = FormatPhoneNumber(phoneNumber);
                _logger.LogInformation("Formatlanmış telefon numarası: {PhoneNumber}", phoneNumber);

                bool result = false;

                switch (_smsSettings.Provider.ToLower())
                {
                    case "netgsm":
                        _logger.LogInformation("NetGSM provider kullanılıyor");
                        result = await SendNetGsmSms(phoneNumber, message);
                        break;
                    case "iletimerkezi":
                        _logger.LogInformation("İletim Merkezi provider kullanılıyor");
                        result = await SendIletiMerkeziSms(phoneNumber, message);
                        break;
                    case "twilio":
                        _logger.LogInformation("Twilio provider kullanılıyor");
                        result = await SendTwilioSms(phoneNumber, message);
                        break;
                    default:
                        _logger.LogError("Desteklenmeyen SMS provider: {Provider}", _smsSettings.Provider);
                        return false;
                }

                if (result)
                {
                    _logger.LogInformation("SMS başarıyla gönderildi: {PhoneNumber}", phoneNumber);
                }
                else
                {
                    _logger.LogWarning("SMS gönderilemedi: {PhoneNumber}", phoneNumber);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS gönderimi başarısız: {PhoneNumber}, Hata: {Error}", phoneNumber, ex.Message);
                return false;
            }
        }

        // PRIVATE helper methods
        private async Task<bool> SendNetGsmSms(string phoneNumber, string message)
        {
            try
            {
                // NetGSM için telefon numarasını düzenle (+ işaretini kaldır)
                var cleanPhone = phoneNumber.Replace("+", "");

                var requestData = new Dictionary<string, string>
                {
                    {"usercode", _smsSettings.Username},
                    {"password", _smsSettings.Password},
                    {"gsmno", cleanPhone},
                    {"message", message},
                    {"msgheader", _smsSettings.Originator}
                };

                var formContent = new FormUrlEncodedContent(requestData);
                var response = await _httpClient.PostAsync(_smsSettings.ApiUrl, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("NetGSM Response: {Response}", responseContent);

                // NetGSM başarı kodları: 00, 01, 02
                return responseContent.StartsWith("00") || responseContent.StartsWith("01") || responseContent.StartsWith("02");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NetGSM SMS gönderimi hatası");
                return false;
            }
        }

        private async Task<bool> SendIletiMerkeziSms(string phoneNumber, string message)
        {
            try
            {
                var requestData = new
                {
                    request = new
                    {
                        authentication = new
                        {
                            username = _smsSettings.Username,
                            password = _smsSettings.Password
                        },
                        order = new
                        {
                            sender = _smsSettings.Originator,
                            sendDateTime = Array.Empty<string>(),
                            iys = 1,
                            iysList = "BIREYSEL",
                            message = new
                            {
                                text = message,
                                receipents = new
                                {
                                    number = new[] { phoneNumber }
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_smsSettings.ApiUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("İletim Merkezi Response: {Response}", responseContent);

                // İletim Merkezi başarı kontrolü
                return response.IsSuccessStatusCode && !responseContent.Contains("error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İletim Merkezi SMS gönderimi hatası");
                return false;
            }
        }

        private async Task<bool> SendTwilioSms(string phoneNumber, string message)
        {
            try
            {
                // Twilio için basic auth
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_smsSettings.Username}:{_smsSettings.Password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

                var requestData = new Dictionary<string, string>
                {
                    {"From", _smsSettings.Originator},
                    {"To", phoneNumber},
                    {"Body", message}
                };

                var formContent = new FormUrlEncodedContent(requestData);
                var response = await _httpClient.PostAsync(_smsSettings.ApiUrl, formContent);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Twilio Response: {Response}", responseContent);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Twilio SMS gönderimi hatası");
                return false;
            }
        }

        private static string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return phoneNumber;

            // Boşlukları ve özel karakterleri temizle
            phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            // 0 ile başlıyorsa kaldır
            if (phoneNumber.StartsWith("0"))
                phoneNumber = phoneNumber.Substring(1);

            // +90 ile başlamıyorsa ekle
            if (!phoneNumber.StartsWith("+90"))
                phoneNumber = "+90" + phoneNumber;

            return phoneNumber;
        }
    }
}