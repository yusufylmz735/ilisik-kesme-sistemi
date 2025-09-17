using System.ComponentModel.DataAnnotations;

namespace HayataAtilmaFormu.ViewModels
{
    // Giriş formu için view model
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Öğrenci numarası veya e-posta adresi gereklidir.")]
        [Display(Name = "Öğrenci No / E-posta")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre gereklidir.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Beni Hatırla")]
        public bool RememberMe { get; set; } = false;
    }

    // Şifre sıfırlama için view model
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "E-posta adresi gereklidir.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;
    }

    // Yeni başvuru oluşturma için view model
    public class CreateApplicationViewModel
    {
        public int OgrenciId { get; set; }

        [Display(Name = "Öğrenci Adı")]
        public string OgrenciAd { get; set; } = string.Empty;

        [Display(Name = "Öğrenci No")]
        public string OgrenciNo { get; set; } = string.Empty;

        [Display(Name = "Fakülte")]
        public string FakulteAdi { get; set; } = string.Empty;

        [Display(Name = "Bölüm")]
        public string BolumAdi { get; set; } = string.Empty;

        [Display(Name = "Eğitim Türü")]
        public string EgitimTuru { get; set; } = string.Empty;

        [Required(ErrorMessage = "Başvuru türü seçmeniz gereklidir.")]
        [Display(Name = "Başvuru Türü")]
        public string BasvuruTuru { get; set; } = string.Empty;

        [Required(ErrorMessage = "Açıklama alanı zorunludur.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Açıklama en az 10, en fazla 1000 karakter olmalıdır.")]
        [Display(Name = "Başvuru Açıklaması")]
        [DataType(DataType.MultilineText)]
        public string Aciklama { get; set; } = string.Empty;

        // Başvuru türü seçenekleri
        public List<string> BasvuruTurleri => new List<string>
        {
        "Mezuniyet",
        "Yatay Geçiş",
        "Dikey Geçiş",
        "Kayıt Dondurma",
        "Kayıt Silme",
        "Nakil",
        "Çekilme",
        "Vefat",
        "Askerlik",
        "Sağlık Raporu",
        "Disiplin Cezası",
        "Akademik Yetersizlik"
        };

        // Başvuru türü açıklamaları
        public Dictionary<string, string> BasvuruTuruAciklamalari => new Dictionary<string, string>
        {
        {"Mezuniyet", "Eğitimi tamamlayarak mezun olma işlemi"},
        {"Yatay Geçiş", "Aynı seviyede başka üniversite/bölüme geçiş"},
        {"Dikey Geçiş", "Önlisanstan lisans programına geçiş"},
        {"Kayıt Dondurma", "Geçici süreliğine eğitime ara verme"},
        {"Kayıt Silme", "Üniversite kaydını tamamen silme"},
        {"Nakil", "Başka üniversiteye nakil işlemi"},
        {"Çekilme", "Kendi isteğiyle üniversiteden ayrılma"},
        {"Vefat", "Öğrencinin vefatı durumu"},
        {"Askerlik", "Askerlik hizmeti nedeniyle"},
        {"Sağlık Raporu", "Sağlık durumu nedeniyle eğitime devam edememe"},
        {"Disiplin Cezası", "Disiplin kurulu kararı ile çıkarılma"},
        {"Akademik Yetersizlik", "Akademik başarısızlık nedeniyle çıkarılma"}
        };
        public Dictionary<string, List<string>> GereklieBelgeler => new Dictionary<string, List<string>>
        {
        {"Vefat", new List<string> {"Vefat belgesi", "Yakın akraba belgesi"}},
        {"Sağlık Raporu", new List<string> {"Heyet raporu", "Hastane belgesi"}},
        {"Askerlik", new List<string> {"Celp belgesi veya askerlik belgesi"}},
        {"Yatay Geçiş", new List<string> {"Kabul belgesi", "Transkript"}},
        {"Dikey Geçiş", new List<string> {"YKS sonuç belgesi", "Diploma fotokopisi"}}
        };

        // Başvuru kategorileri (görsel gruplandırma için)
        public Dictionary<string, List<string>> BasvuruKategorileri => new Dictionary<string, List<string>>
        {
        {"Normal İşlemler", new List<string> {"Mezuniyet", "Yatay Geçiş", "Dikey Geçiş", "Nakil"}},
        {"Kayıt İşlemleri", new List<string> {"Kayıt Dondurma", "Kayıt Silme", "Çekilme"}},
        {"Özel Durumlar", new List<string> {"Vefat", "Askerlik", "Sağlık Raporu"}},
        {"Zorunlu Çıkış", new List<string> {"Disiplin Cezası", "Akademik Yetersizlik"}}
        };
    }


    // Başvuru detayları ve takip için view model
    public class ApplicationDetailsViewModel
    {
        public int BasvuruId { get; set; }

        [Display(Name = "Başvuru Türü")]
        public string BasvuruTuru { get; set; } = string.Empty;

        [Display(Name = "Açıklama")]
        public string Aciklama { get; set; } = string.Empty;

        [Display(Name = "Başvuru Tarihi")]
        [DataType(DataType.DateTime)]
        public DateTime BasvuruTarihi { get; set; }

        [Display(Name = "Durum")]
        public string Durum { get; set; } = string.Empty;

        [Display(Name = "Mevcut Aşama")]
        public int MevcutAsama { get; set; }

        [Display(Name = "Toplam Aşama")]
        public int ToplamAsama { get; set; }

        [Display(Name = "Tamamlanma Tarihi")]
        [DataType(DataType.DateTime)]
        public DateTime? TamamlanmaTarihi { get; set; }

        [Display(Name = "Red Nedeni")]
        public string RedNedeni { get; set; } = string.Empty;

        public bool PdfDosyaVarMi { get; set; }

        // Onay detayları
        public List<ApprovalStepViewModel> OnayDetaylari { get; set; } = new List<ApprovalStepViewModel>();

        // İlerleme yüzdesi
        public int IlerlemeYuzdesi => ToplamAsama > 0 ? (int)Math.Round((double)MevcutAsama / ToplamAsama * 100) : 0;

        // Durum rengi
        public string DurumRengi => Durum?.ToLower() switch
        {
            "beklemede" => "warning",
            "onaylandi" => "success",
            "tamamlandi" => "success",
            "reddedildi" => "danger",
            _ => "secondary"
        };

        // Durum ikonu
        public string DurumIkonu => Durum?.ToLower() switch
        {
            "beklemede" => "clock",
            "onaylandi" => "check-circle",
            "tamamlandi" => "check-circle-fill",
            "reddedildi" => "x-circle",
            _ => "question-circle"
        };

        // Durum açıklaması
        public string DurumAciklamasi => Durum?.ToLower() switch
        {
            "beklemede" => "Başvurunuz değerlendiriliyor",
            "onaylandi" => "Başvurunuz onaylandı",
            "tamamlandi" => "İşlemleriniz tamamlandı",
            "reddedildi" => "Başvurunuz reddedildi",
            _ => "Durum bilinmiyor"
        };
    }

    // Onay aşamalarının detayları için view model
    public class ApprovalStepViewModel
    {
        [Display(Name = "Aşama No")]
        public int AsamaNo { get; set; }

        [Display(Name = "Aşama")]
        public string AsamaAdi { get; set; } = string.Empty;

        [Display(Name = "Durum")]
        public string Durum { get; set; } = string.Empty;

        [Display(Name = "Onay Tarihi")]
        [DataType(DataType.DateTime)]
        public DateTime? OnayTarihi { get; set; }

        [Display(Name = "Yetkili")]
        public string YetkiliAdi { get; set; } = string.Empty;

        [Display(Name = "Açıklama")]
        public string Aciklama { get; set; } = string.Empty;

        // Aşama durumu rengi
        public string DurumRengi => Durum?.ToLower() switch
        {
            "beklemede" => "warning",
            "onaylandi" => "success",
            "reddedildi" => "danger",
            "bekleniyor" => "secondary",
            _ => "light"
        };

        // Aşama durumu ikonu
        public string DurumIkonu => Durum?.ToLower() switch
        {
            "beklemede" => "clock",
            "onaylandi" => "check-circle-fill",
            "reddedildi" => "x-circle-fill",
            "bekleniyor" => "circle",
            _ => "question-circle"
        };

        // Aşamanın aktif olup olmadığını belirler
        public bool IsActive => string.Equals(Durum, "Beklemede", StringComparison.OrdinalIgnoreCase);

        // Aşamanın tamamlanıp tamamlanmadığını belirler
        public bool IsCompleted => string.Equals(Durum, "Onaylandi", StringComparison.OrdinalIgnoreCase);

        // Aşamanın reddedilip reddedilmediğini belirler
        public bool IsRejected => string.Equals(Durum, "Reddedildi", StringComparison.OrdinalIgnoreCase);

        // Aşama açıklaması
        public string DurumAciklamasi => Durum?.ToLower() switch
        {
            "beklemede" => "İnceleniyor",
            "onaylandi" => "Onaylandı",
            "reddedildi" => "Reddedildi",
            "bekleniyor" => "Sırada bekliyor",
            _ => "Bilinmiyor"
        };
    }

    // Genel başvuru istatistikleri için view model
    public class ApplicationStatsViewModel
    {
        [Display(Name = "Toplam Başvuru")]
        public int ToplamBasvuru { get; set; }

        [Display(Name = "Bekleyen Başvuru")]
        public int BekleyenBasvuru { get; set; }

        [Display(Name = "Onaylanan Başvuru")]
        public int OnaylananBasvuru { get; set; }

        [Display(Name = "Reddedilen Başvuru")]
        public int ReddedilenBasvuru { get; set; }

        [Display(Name = "Tamamlanan Başvuru")]
        public int TamamlananBasvuru { get; set; }

        public double OnayOrani => ToplamBasvuru > 0 ? Math.Round((double)OnaylananBasvuru / ToplamBasvuru * 100, 2) : 0;

        public double RedOrani => ToplamBasvuru > 0 ? Math.Round((double)ReddedilenBasvuru / ToplamBasvuru * 100, 2) : 0;

        public double TamamlanmaOrani => ToplamBasvuru > 0 ? Math.Round((double)TamamlananBasvuru / ToplamBasvuru * 100, 2) : 0;
    }

    // Yetkili paneli için başvuru listesi view model'i
    public class AuthorityApplicationListViewModel
    {
        public int BasvuruId { get; set; }

        [Display(Name = "Öğrenci No")]
        public string OgrenciNo { get; set; } = string.Empty;

        [Display(Name = "Öğrenci Adı")]
        public string OgrenciAdi { get; set; } = string.Empty;

        [Display(Name = "Bölüm")]
        public string BolumAdi { get; set; } = string.Empty;

        [Display(Name = "Başvuru Türü")]
        public string BasvuruTuru { get; set; } = string.Empty;

        [Display(Name = "Başvuru Tarihi")]
        [DataType(DataType.Date)]
        public DateTime BasvuruTarihi { get; set; }

        [Display(Name = "Mevcut Aşama")]
        public string MevcutAsamaAdi { get; set; } = string.Empty;

        [Display(Name = "Durum")]
        public string Durum { get; set; } = string.Empty;

        // Durum rengi
        public string DurumRengi => Durum?.ToLower() switch
        {
            "beklemede" => "warning",
            "onaylandi" => "success",
            "reddedildi" => "danger",
            _ => "secondary"
        };

        // Beklemede olan gün sayısı
        public int BeklemeGunSayisi => (DateTime.Now - BasvuruTarihi).Days;
    }

    // Yetkili onay işlemi için view model
    public class ApprovalActionViewModel
    {
        public int BasvuruId { get; set; }
        public int OnayDetayId { get; set; }

        [Display(Name = "İşlem")]
        [Required(ErrorMessage = "İşlem seçimi gereklidir.")]
        public string Action { get; set; } = string.Empty; // "Onayla" veya "Reddet"

        [Display(Name = "Açıklama")]
        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string Aciklama { get; set; } = string.Empty;

        // Başvuru bilgileri (gösterim için)
        public string OgrenciAdi { get; set; } = string.Empty;
        public string OgrenciNo { get; set; } = string.Empty;
        public string BasvuruTuru { get; set; } = string.Empty;
        public string AsamaAdi { get; set; } = string.Empty;
    }

    // Form validation için özel attribute'lar
    public class BasvuruTuruValidationAttribute : ValidationAttribute
    {
        private static readonly string[] ValidTurler = { "Mezuniyet", "Yatay Geçiş", "Dikey Geçiş" };

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string tur && ValidTurler.Contains(tur, StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult("Geçerli bir başvuru türü seçiniz.");
        }
    }

    // Başvuru form wizard için adım view model'i
    public class ApplicationStepViewModel
    {
        public int StepNumber { get; set; }

        [Display(Name = "Adım Başlığı")]
        public string StepTitle { get; set; } = string.Empty;

        [Display(Name = "Açıklama")]
        public string StepDescription { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }
        public bool IsActive { get; set; }
        public bool IsAccessible { get; set; }

        public string StepClass => IsCompleted ? "completed" : IsActive ? "active" : IsAccessible ? "accessible" : "disabled";
        public string StepIcon => IsCompleted ? "check-circle-fill" : IsActive ? "circle-fill" : "circle";
    }

    // Başvuru onay geçmişi için view model
    public class ApprovalHistoryViewModel
    {
        public int BasvuruId { get; set; }

        [Display(Name = "Öğrenci")]
        public string OgrenciAd { get; set; } = string.Empty;

        [Display(Name = "Öğrenci No")]
        public string OgrenciNo { get; set; } = string.Empty;

        [Display(Name = "Fakülte")]
        public string FakulteAdi { get; set; } = string.Empty;

        [Display(Name = "Bölüm")]
        public string BolumAdi { get; set; } = string.Empty;

        [Display(Name = "Başvuru Tarihi")]
        [DataType(DataType.Date)]
        public DateTime BasvuruTarihi { get; set; }

        [Display(Name = "Başvuru Türü")]
        public string BasvuruTuru { get; set; } = string.Empty;

        [Display(Name = "Son Durum")]
        public string FinalDurum { get; set; } = string.Empty;

        [Display(Name = "Tamamlanma Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? TamamlanmaTarihi { get; set; }

        public List<ApprovalStepViewModel> OnayAsamalari { get; set; } = new List<ApprovalStepViewModel>();

        // İşlem süresi hesaplama
        public TimeSpan? IslemSuresi => TamamlanmaTarihi.HasValue ?
            TamamlanmaTarihi.Value - BasvuruTarihi : null;

        public string IslemSuresiMetni => IslemSuresi.HasValue ?
            $"{IslemSuresi.Value.Days} gün" : "Devam ediyor";
    }

    // Özel validation attribute'ları
    public class FutureOrPresentDateAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime date && date < DateTime.Today)
            {
                return new ValidationResult("Tarih bugünden önce olamaz.");
            }
            return ValidationResult.Success;
        }
    }
}