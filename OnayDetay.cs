using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HayataAtilmaFormu.Models
{
    public class OnayDetay
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BasvuruId { get; set; }

        [Required]
        public int AsamaNo { get; set; }

        [Required]
        [StringLength(100)]
        public string AsamaAdi { get; set; } = string.Empty;

        public int? YetkiliId { get; set; }

        [Required]
        [StringLength(20)]
        public string Durum { get; set; } = ApplicationConstants.Beklemede;

        public DateTime? OnayTarihi { get; set; }

        [StringLength(1000)]
        public string Aciklama { get; set; } = string.Empty;

        public int? BeklenenFakulteId { get; set; }

        [StringLength(50)]
        public string? YetkiliPozisyonu { get; set; }

        [StringLength(100)]
        public string? YetkiliAdi { get; set; } // Yetkili adı için yeni alan

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        [ForeignKey("BasvuruId")]
        public virtual Basvuru Basvuru { get; set; } = null!;

        [ForeignKey("YetkiliId")]
        public virtual Yetkili? Yetkili { get; set; }

        [ForeignKey("BeklenenFakulteId")]
        public virtual Fakulte? BeklenenFakulte { get; set; }

        [NotMapped]
        public bool IsCompleted => Durum == ApplicationConstants.Onaylandi || Durum == ApplicationConstants.Reddedildi;

        [NotMapped]
        public bool IsPending => Durum == ApplicationConstants.Beklemede;

        [NotMapped]
        public bool IsCancelled => Durum == ApplicationConstants.Iptal;
    }
}