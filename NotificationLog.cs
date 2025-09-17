using System.ComponentModel.DataAnnotations;

namespace HayataAtilmaFormu.Models
{
    public class NotificationLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Channel { get; set; } = string.Empty; // EMAIL, SMS

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty; // OGRENCI_ONAY, YETKILI_ONAY etc.

        [Required]
        [StringLength(200)]
        public string Recipient { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;

        public bool Success { get; set; }

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        public DateTime SentAt { get; set; }
    }
}