using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HayataAtilmaFormu.Models
{
    public class Bolum
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string BolumKodu { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string BolumAdi { get; set; } = string.Empty;

        public bool Aktif { get; set; }

        [Required]
        public int FakulteId { get; set; }

        public Fakulte? Fakulte { get; set; }  

        [StringLength(50)]
        public string? EgitimTuru { get; set; } 

        public List<Ogrenci> Ogrenciler { get; set; } = new List<Ogrenci>();
        public List<Yetkili> Yetkililer { get; set; } = new List<Yetkili>(); // Yeni navigation property
        public string Aciklama { get; internal set; } = string.Empty;
    }
}