using System.ComponentModel.DataAnnotations;

namespace Ezz_api.ViewModel
{
    public class ContactFormData
    {
        [Required, MinLength(2)]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(10)]
        public string Message { get; set; }
    }
}
