using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class Yetkili
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Sifre { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string AdSoyad { get; set; } = string.Empty;

        [Required]
        public int FakulteId { get; set; }

        public int? BolumId { get; set; } // Nullable hale getirildi

        [Required]
        [StringLength(100)]
        public string Pozisyon { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string OnayAsamasi { get; set; } = string.Empty;

        public UserProfile? UserProfile { get; set; }

        [StringLength(20)]
        public string Rol { get; set; } = "Yetkili";

        public bool Aktif { get; set; } = true;

        // Onay sistemi alanları
        public bool OnayBekliyor { get; set; } = true;

        public int? OnaylayanAdminId { get; set; }

        public DateTime? OnayTarihi { get; set; }

        [StringLength(500)]
        public string? RedNedeni { get; set; }

        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        // Ek alanlar
        [StringLength(20)]
        public string? Telefon { get; set; }

        [StringLength(1000)]
        public string? Aciklama { get; set; }

        // Performans alanları
        public int ToplamOnayladigiBasvuru { get; set; } = 0;

        public int ToplamReddettigiBasvuru { get; set; } = 0;

        public DateTime? SonAktiviteTarihi { get; set; }

        public double? OrtalamaCevapSuresi { get; set; }

        // Navigation Properties
        [ForeignKey("FakulteId")]
        public virtual Fakulte Fakulte { get; set; } = null!;

        [ForeignKey("BolumId")]
        public virtual Bolum? Bolum { get; set; } // Navigation eklendi

        [ForeignKey("OnaylayanAdminId")]
        public virtual Yetkili? OnaylayanAdmin { get; set; }

        public virtual ICollection<OnayDetay> OnayDetaylari { get; set; } = new List<OnayDetay>();

        public virtual ICollection<Basvuru> SonIslemYaptigiBasvurular { get; set; } = new List<Basvuru>();

        // Yardımcı özellikler
        [NotMapped]
        public bool IsActive => Aktif && !OnayBekliyor;

        [NotMapped]
        public int ToplamIslem => ToplamOnayladigiBasvuru + ToplamReddettigiBasvuru;

        [NotMapped]
        public double? OnayOrani => ToplamIslem > 0 ? (double)ToplamOnayladigiBasvuru / ToplamIslem * 100 : null;

        [NotMapped]
        public string PerformanceStatus =>
            OrtalamaCevapSuresi.HasValue ?
                OrtalamaCevapSuresi.Value <= 3 ? "Hızlı" :
                OrtalamaCevapSuresi.Value <= 7 ? "Normal" : "Yavaş"
                : "Belirsiz";

        public DateTime GuncellenmeTarihi { get; internal set; }
    }
}