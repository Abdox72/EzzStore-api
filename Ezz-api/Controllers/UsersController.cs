using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ezz_api.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            // 1) Materialize the users
            var allUsers = await _userManager.Users.ToListAsync();

            // 2) Build your DTOs, fetching roles per user
            var usersWithRoles = new List<object>();
            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                usersWithRoles.Add(new
                {
                    id = u.Id,
                    username = u.UserName,
                    email = u.Email,
                    name = u.FullName,
                    // safely take first role or null/empty
                    role = roles.FirstOrDefault() ?? string.Empty
                });
            }

            // 3) Return
            return Ok(usersWithRoles);
        }

        // GET: api/Users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                Roles = roles
            });
        }

        // PUT: api/Users/{id}/role
        [HttpPut("{id}/role")]
        public async Task<IActionResult> UpdateUserRole(string id, [FromBody] string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            var result = await _userManager.AddToRoleAsync(user, role);

            if (!result.Succeeded) return BadRequest(result.Errors);
            return NoContent();
        }

        // DELETE: api/Users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return NoContent();
        }

        // POST : api/users
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AddUserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray() });
            }

            if (await _userManager.FindByEmailAsync(userDto.Email) != null)
            {
                return BadRequest(new { message = "Email already in use." });
            }

            var user = new ApplicationUser { UserName = userDto.Email, Email = userDto.Email, FullName = userDto.Name };
            var result = await _userManager.CreateAsync(user, userDto.Password);
            if (!result.Succeeded) return BadRequest(new { message = "Failed To Create User" });
           

            //bind the role
            var userRole = userDto.Role?.ToLower() == "admin" ? "Admin" : "User"; 
            await _userManager.AddToRoleAsync(user, userRole);

            if (result.Succeeded)
            {
                return Ok(new { message = "User Added successfully." });
            }

            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });

        }
    }

    // add user dto
    public class AddUserDto
    {
        [StringLength(50, ErrorMessage = "Name cannot be longer than 50 characters.")]
        public string? Name { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        public string? Role { get; set; } = "User";
        [PasswordPropertyText]
        public required string Password { get; set; }
    }
}