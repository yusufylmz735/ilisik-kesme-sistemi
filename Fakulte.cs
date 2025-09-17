using System.ComponentModel.DataAnnotations;

namespace HayataAtilmaFormu.Models
{
    public class Fakulte
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FakulteAdi { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string FakulteKodu { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string FakulteTuru { get; set; } = string.Empty;

        public string Aciklama { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;

        public virtual ICollection<Bolum> Bolumler { get; set; } = new List<Bolum>();
        public virtual ICollection<Ogrenci> Ogrenciler { get; set; } = new List<Ogrenci>();
        public virtual ICollection<Yetkili> Yetkililer { get; set; } = new List<Yetkili>();
    }
}