using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Ezz_api.ViewModel
{
    public class RegisterModel
    {
        [StringLength(50, ErrorMessage = "Name cannot be longer than 50 characters.")]
        public string? Name { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        [PasswordPropertyText]
        public required string Password { get; set; }
    }
}
