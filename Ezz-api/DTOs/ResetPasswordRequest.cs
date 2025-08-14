using System.ComponentModel.DataAnnotations;

namespace Ezz_api.DTOs
{
    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string NewPassword { get; set; }
    }
}
