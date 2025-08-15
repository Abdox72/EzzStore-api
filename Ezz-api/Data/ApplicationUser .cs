using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}