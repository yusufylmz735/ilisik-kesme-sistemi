using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class OnayAsama
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string AsamaAdi { get; set; } = string.Empty;

        [Required]
        public int SiraNo { get; set; }

        [Required]
        [StringLength(50)]
        public string OgrenciTuru { get; set; } = string.Empty; // Lisans, Önlisans, Yüksek Lisans, Doktora

        public bool Ortak { get; set; } = false; // Ortak aşama mı?

        [StringLength(1000)]
        public string Aciklama { get; set; } = string.Empty;

        public bool Aktif { get; set; } = true;

        [StringLength(100)]
        public string YetkiliPozisyonu { get; set; } = string.Empty;

        public bool FakulteBazli { get; set; } = true; // Fakülte bazında mı değil mi?

        public int MaxSure { get; set; } = 7; // Maksimum süre (gün)

        public bool ZorunluAciklama { get; set; } = false; // Açıklama zorunlu mu?

        // YENİ ALANLAR - Yetkili Sınırlaması İçin
        [Required]
        public int MaxYetkiliSayisi { get; set; } = 1; // Maksimum yetkili sayısı

        public bool BolumBazli { get; set; } = false; // Bölüm bazında mı? (Fakülte bazlı aşamalar için)

        // Yardımcı özellikler
        [NotMapped]
        public bool IsCommonStage => Ortak; // Ortak aşama mı?

        [NotMapped]
        public bool IsFacultyBasedStage => FakulteBazli && !Ortak; // Fakülte bazlı aşama mı?

        [NotMapped]
        public bool IsDepartmentBasedStage => BolumBazli && FakulteBazli && !Ortak; // Bölüm bazlı aşama mı?

        [NotMapped]
        public string ScopeDescription
        {
            get
            {
                if (Ortak) return "Üniversite Geneli";
                if (BolumBazli && FakulteBazli) return "Bölüm Bazlı";
                if (FakulteBazli) return "Fakülte Bazlı";
                return "Genel";
            }
        }

        [NotMapped]
        public string MaxAuthorityDescription => $"Maksimum {MaxYetkiliSayisi} yetkili";
    }
}