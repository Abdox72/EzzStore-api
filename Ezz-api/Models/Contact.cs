using System.ComponentModel.DataAnnotations;

namespace Ezz_api.Models
{
    public class Contact    
    {
    
    public int Id { get; set; }

    [Required, MinLength(2)]
    public string Name { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, MinLength(10)]
    public string Message { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; }
    }
}
