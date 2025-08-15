using Ezz_api.DTOs;
using Ezz_api.Services;
using Ezz_api.Utilities;
using Ezz_api.ViewModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ITokenService _tokenService;
        private readonly LinkGenerator _linkGenerator;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ITokenService tokenService,
            LinkGenerator linkGenerator,
            IConfiguration config,
            HttpClient httpClient)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _tokenService = tokenService;
            _linkGenerator = linkGenerator;
            _config = config;
            _httpClient = httpClient;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (await _userManager.FindByEmailAsync(model.Email) != null)
                return Conflict(new { message = "Email already registered." });

            var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.Name ,CreatedAt=DateTime.Now};
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = GenerateUrl("ConfirmEmail", new { userId = user.Id, token });
            await _emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm by <a href='{callbackUrl}'>clicking here</a>.");

            return Ok(new { message = "Registration successful. Check your email." });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                // Verify the Google ID token
                var googleUserInfo = await VerifyGoogleToken(model.IdToken);
                if (googleUserInfo == null)
                    return Unauthorized(new { message = "Invalid Google token." });

                // Check if user exists
                var user = await _userManager.FindByEmailAsync(googleUserInfo.Email);
                if (user == null)
                {
                    // Create new user
                    user = new ApplicationUser
                    {
                        UserName = googleUserInfo.Email,
                        Email = googleUserInfo.Email,
                        FullName = googleUserInfo.Name,
                        EmailConfirmed = true, // Google emails are pre-verified
                        ProfilePictureUrl = googleUserInfo.Picture
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                        return BadRequest(result.Errors);
                }

                // Generate JWT token
                var roles = await _userManager.GetRolesAsync(user);
                var token = _tokenService.CreateToken(user, roles);

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Google authentication failed." });
            }
        }

        private async Task<GoogleUserInfo?> VerifyGoogleToken(string idToken)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");
                var content = await response.Content.ReadAsStringAsync();

                // log raw response for debugging
                Console.WriteLine("tokeninfo raw: " + content);

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfo>(content, options);


                    if (tokenInfo != null && tokenInfo.Aud == _config["Google:ClientId"])
                    {
                        return new GoogleUserInfo
                        {
                            Email = tokenInfo.Email,
                            Name = tokenInfo.Name,
                            Picture = tokenInfo.Picture
                        };
                    }

                    // optional: log mismatch
                    Console.WriteLine("aud mismatch: tokenInfo.Aud='" + tokenInfo?.Aud + "' config='" + _config["Google:ClientId"] + "'");
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("VerifyGoogleToken error: " + ex);
                return null;
            }
        }


        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var decodedToken = TokenHelper.Decode(token);

            var result= await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded) return BadRequest(new { message = "Invalid or expired token." });

            return Ok(new { message = "Email confirmed." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                return Unauthorized(new { message = "Invalid credentials." });

            if (!user.EmailConfirmed)
                return Unauthorized(new { message = "Email not confirmed." });

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.CreateToken(user, roles);

            return Ok(new { token });
        }

        // Common endpoint for sending tokens
        [HttpPost("send-token/{purpose}")]
        public async Task<IActionResult> SendToken([FromRoute] string purpose, [FromBody] EmailRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok();

            switch (purpose.ToLower())
            {
                case "verify":
                    if (user.EmailConfirmed) return Conflict(new { message = "Already confirmed." });
                    var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await SendLink(user.Email, "Confirm your email", GenerateUrl("ConfirmEmail", new { userId = user.Id, token = confirmToken }));
                    break;
                case "reset":
                    if (!user.EmailConfirmed) return Ok();
                    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await SendLink(user.Email, "Reset Password", GenerateUrl("ResetPassword", new { email = user.Email, token = resetToken }));
                    break;
                default:
                    return BadRequest(new { message = "Invalid purpose." });
            }

            return Ok();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return NotFound();

            var decodedToken = TokenHelper.Decode(request.Token);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { message = "Password reset successful." });
        }
        private string GenerateUrl(string action, object values)
        {
            var baseUrl = _config["AppSettings:ClientUrl"]!;
            //i use angular reset form so lets use baseurl 
            if (action == "ResetPassword")
            {
                var email = (string)values.GetType().GetProperty("email")!.GetValue(values)!;
                var rawToken = (string)values.GetType().GetProperty("token")!.GetValue(values)!;
                var encodedToken = TokenHelper.Encode(rawToken);
                return $"{baseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={encodedToken}";
            }
            else if(action == "ConfirmEmail")
            {
                var userId = (string)values.GetType().GetProperty("userId")!.GetValue(values)!;
                var rawToken = (string)values.GetType().GetProperty("token")!.GetValue(values)!;

                var encodedToken = TokenHelper.Encode(rawToken);

                return $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(userId)}&token={encodedToken}";
            }

            //if server handle ??
            return _linkGenerator.GetUriByAction(HttpContext, action, "Auth", values)
                   ?? throw new InvalidOperationException("Cannot generate URL");
        }
        private Task SendLink(string email, string subject, string link)
            => _emailSender.SendEmailAsync(email, subject, $"<a href='{link}'>Click here</a>");
    }

    public class GoogleLoginModel
    {
        public string IdToken { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
    }

    public class GoogleTokenInfo
    {
        public string Aud { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
    }
}
