using System.ComponentModel.DataAnnotations;

namespace ProfilSayfam.Models
{
    public class ContactForm
    {
        [Required(ErrorMessage = "Ad Soyad alanı zorunludur.")]
        [StringLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir.")]
        [Display(Name = "Ad Soyad")]
        public string Name { get; set; }

        [Required(ErrorMessage = "E-posta alanı zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(100, ErrorMessage = "E-posta en fazla 100 karakter olabilir.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Konu alanı zorunludur.")]
        [StringLength(200, ErrorMessage = "Konu en fazla 200 karakter olabilir.")]
        [Display(Name = "Konu")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Mesaj alanı zorunludur.")]
        [StringLength(2000, ErrorMessage = "Mesaj en fazla 2000 karakter olabilir.")]
        [Display(Name = "Mesaj")]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public string Status { get; set; } = "New";
    }
}