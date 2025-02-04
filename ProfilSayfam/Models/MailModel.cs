using System.ComponentModel.DataAnnotations;

namespace ProfilSayfam.Models
{
    public class MailModel
    {
        [Required(ErrorMessage = "NameRequired")]
        [StringLength(100, ErrorMessage = "NameLength")]
        public string Name { get; set; }

        [Required(ErrorMessage = "EmailRequired")]
        [EmailAddress(ErrorMessage = "EmailFormat")]
        public string Mail { get; set; }

        [Required(ErrorMessage = "SubjectRequired")]
        [StringLength(200, ErrorMessage = "SubjectLength")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "MessageRequired")]
        [StringLength(2000, ErrorMessage = "MessageLength")]
        public string Message { get; set; }
    }
}
