using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class Ogrenci
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string OgrenciNo { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Ad { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Soyad { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Sifre { get; set; } = string.Empty;

        [Required]
        public int FakulteId { get; set; }

        [Required]
        public int BolumId { get; set; }

        public string Telefon { get; set; } = string.Empty;
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        public bool Aktif { get; set; } = true; // Eklenen özellik

        [ForeignKey("FakulteId")]
        public virtual Fakulte Fakulte { get; set; } = null!;

        [ForeignKey("BolumId")]
        public virtual Bolum Bolum { get; set; } = null!;
        
        [Display(Name = "Son Güncellenme Tarihi")]
        public DateTime? GuncellenmeTarihi { get; set; }

        public virtual ICollection<Basvuru> Basvurular { get; set; } = new List<Basvuru>();

        [NotMapped]
        public string OgrenciTuru => Bolum?.EgitimTuru ?? string.Empty;

        [NotMapped]
        public string TamAd => $"{Ad} {Soyad}";
        public required UserProfile UserProfile { get; set; }
    }
}