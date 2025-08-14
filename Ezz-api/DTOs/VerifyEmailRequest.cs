using System.ComponentModel.DataAnnotations;

namespace Ezz_api.DTOs
{
    public class VerifyEmailRequest : EmailRequest
    {
        [Required]
        public string Token { get; set; }
    }
}
