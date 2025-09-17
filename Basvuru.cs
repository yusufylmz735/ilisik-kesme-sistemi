using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class Basvuru
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OgrenciId { get; set; }

        [Required]
        [StringLength(100)]
        public string BasvuruTuru { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Aciklama { get; set; } = string.Empty;

        [Required]
        public DateTime BasvuruTarihi { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Durum { get; set; } = "Beklemede";

        [Required]
        public int MevcutAsama { get; set; } = 1;

        [Required]
        public int ToplamAsama { get; set; } = 8;

        public DateTime? TamamlanmaTarihi { get; set; }

        [StringLength(1000)]
        public string? RedNedeni { get; set; }

        // YENİ ALANLAR - Gelişmiş takip için
        public DateTime? SonGuncellemeTarihi { get; set; }

        public int? SonIslemYapanYetkiliId { get; set; }

        [StringLength(50)]
        public string? OncelikDurumu { get; set; } = "Normal";

        // YENİ DOSYA ALANLARI
        [StringLength(500)]
        public string? DosyaYolu { get; set; }

        [StringLength(255)]
        public string? OriginalDosyaAdi { get; set; }

        [StringLength(10)]
        public string? DosyaUzantisi { get; set; }

        public long? DosyaBoyutu { get; set; } // Byte cinsinden

        public DateTime? DosyaYuklemeTarihi { get; set; }

        [StringLength(50)]
        public string? DosyaContentType { get; set; }

        // Navigation Properties
        [ForeignKey("OgrenciId")]
        public virtual Ogrenci Ogrenci { get; set; } = null!;

        [ForeignKey("SonIslemYapanYetkiliId")]
        public virtual Yetkili? SonIslemYapanYetkili { get; set; }

        public virtual ICollection<OnayDetay> OnayDetaylari { get; set; } = new List<OnayDetay>();

        // Yardımcı özellikler
        [NotMapped]
        public bool IsCompleted => Durum == "Onaylandi" || Durum == "Reddedildi";

        [NotMapped]
        public bool IsPending => Durum == "Beklemede";

        [NotMapped]
        public bool IsApproved => Durum == "Onaylandi";

        [NotMapped]
        public bool IsRejected => Durum == "Reddedildi";

        [NotMapped]
        public double CompletionPercentage => ToplamAsama > 0 ? (double)MevcutAsama / ToplamAsama * 100 : 0;

        [NotMapped]
        public int RemainingSteps => Math.Max(0, ToplamAsama - MevcutAsama);

        [NotMapped]
        public TimeSpan ProcessingTime =>
            (TamamlanmaTarihi ?? DateTime.Now) - BasvuruTarihi;

        [NotMapped]
        public string ProcessingTimeText =>
            $"{ProcessingTime.Days} gün {ProcessingTime.Hours} saat";

        // YENİ DOSYA HELPER METHODLARI
        [NotMapped]
        public bool HasFile => !string.IsNullOrEmpty(DosyaYolu);

        [NotMapped]
        public string? DosyaBoyutuText
        {
            get
            {
                if (DosyaBoyutu == null) return null;

                var boyut = DosyaBoyutu.Value;
                if (boyut < 1024)
                    return $"{boyut} B";
                else if (boyut < 1024 * 1024)
                    return $"{boyut / 1024.0:F1} KB";
                else
                    return $"{boyut / (1024.0 * 1024.0):F1} MB";
            }
        }

        [NotMapped]
        public string? SafeFileName
        {
            get
            {
                if (string.IsNullOrEmpty(OriginalDosyaAdi)) return null;
                return $"{Ogrenci?.OgrenciNo}_{Id}_{DateTime.Now:yyyyMMdd_HHmmss}{DosyaUzantisi}";
            }
        }
    }
}