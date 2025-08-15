using System.ComponentModel.DataAnnotations;

namespace Ezz_api.DTOs
{
    public class EmailRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }
}
