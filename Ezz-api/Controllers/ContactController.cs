using Ezz_api.Services;
using Ezz_api.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;

        public ContactController(IContactService contactService)
        {
            _contactService = contactService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Post([FromBody] ContactFormData data)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(); 
            await _contactService.SendContactAsync(data, userId!);

            return Ok(new { message = "Contact message submitted successfully" });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var contacts = await _contactService.GetAllContactsAsync();
            if (contacts == null || !contacts.Any())
                return NotFound(new { message = "No contact messages found" });
            return Ok(contacts);
        }
        //delete a contact message by id
        [HttpDelete]
        [Route("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var contact = await _contactService.DeleteContact(id);
            if (contact == null)
                return NotFound(new { message = "Contact message not found" });
            return Ok(new { message = "Contact message deleted successfully" });
        }
    }
}
