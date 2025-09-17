using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class UserProfile
    {
        [Key]
        public int Id { get; set; }

        public int? OgrenciId { get; set; }

        public int? YetkiliId { get; set; }

        [StringLength(500)]
        public string? ProfilFotoYolu { get; set; }

        [StringLength(1000)]
        public string? Hakkinda { get; set; }

        [StringLength(100)]
        public string? Website { get; set; }

        [StringLength(100)]
        public string? LinkedIn { get; set; }

        [StringLength(100)]
        public string? Twitter { get; set; }

        [StringLength(200)]
        public string? Adres { get; set; }

        [StringLength(50)]
        public string? DogumTarihi { get; set; }

        [StringLength(20)]
        public string? Cinsiyet { get; set; }

        [StringLength(100)]
        public string? Universite { get; set; }

        [StringLength(100)]
        public string? Mezuniyet { get; set; }

        public bool ProfilTamamlandi { get; set; } = false;

        public DateTime GuncellenmeTarihi { get; set; } = DateTime.Now;

        [ForeignKey("OgrenciId")]
        public virtual Ogrenci? Ogrenci { get; set; }

        [ForeignKey("YetkiliId")]
        public virtual Yetkili? Yetkili { get; set; }
    }
}