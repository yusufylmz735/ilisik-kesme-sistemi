using System.ComponentModel.DataAnnotations;

namespace HayataAtilmaFormu.Models.ViewModels
{
    public class ProfileViewModel
    {
        public int Id { get; set; }
        public string UserType { get; set; } = string.Empty;

        [Display(Name = "Ad Soyad")]
        public string AdSoyad { get; set; } = string.Empty;

        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Telefon")]
        public string? Telefon { get; set; }

        [Display(Name = "Fakülte")]
        public int FakulteId { get; set; } // Eklendi
        public string FakulteAdi { get; set; } = string.Empty;

        [Display(Name = "Bölüm")]
        public int? BolumId { get; set; } // Eklendi
        public string? BolumAdi { get; set; }

        [Display(Name = "Eğitim Türü")]
        public string? EgitimTuru { get; set; }

        [Display(Name = "Öğrenci No")]
        public string? OgrenciNo { get; set; }

        [Display(Name = "Pozisyon")]
        public string? Pozisyon { get; set; }

        [Display(Name = "Onay Aşaması")]
        public string? OnayAsamasi { get; set; }

        [Display(Name = "Rol")]
        public string? Rol { get; set; }

        [Display(Name = "Profil Fotoğrafı")]
        public string? ProfilFotoYolu { get; set; }

        [Display(Name = "Hakkında")]
        [StringLength(1000)]
        public string? Hakkinda { get; set; }

        [Display(Name = "Website")]
        [Url]
        public string? Website { get; set; }

        [Display(Name = "LinkedIn")]
        public string? LinkedIn { get; set; }

        [Display(Name = "Twitter")]
        public string? Twitter { get; set; }

        [Display(Name = "Adres")]
        [StringLength(200)]
        public string? Adres { get; set; }

        [Display(Name = "Doğum Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? DogumTarihi { get; set; }

        [Display(Name = "Cinsiyet")]
        public string? Cinsiyet { get; set; }

        [Display(Name = "Mezun Olduğu Üniversite")]
        public string? Universite { get; set; }

        [Display(Name = "Mezuniyet Yılı")]
        public string? Mezuniyet { get; set; }

        [Display(Name = "Açıklama")]
        public string? Aciklama { get; set; }

        public bool ProfilTamamlandi { get; set; }

        public DateTime KayitTarihi { get; set; }
        public string? AnneAdi { get; set; }
        public string? BabaAdi { get; set; }
        public string? Unvan { get; set; }

        public DateTime GuncellenmeTarihi { get; set; }
    }
}