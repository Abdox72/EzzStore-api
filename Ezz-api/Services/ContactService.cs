using Ezz_api.Models;
using Ezz_api.ViewModel;
using Microsoft.Extensions.Logging;

namespace Ezz_api.Services
{
    public class ContactService : IContactService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ContactService> _logger;

        public ContactService(ApplicationDbContext db, ILogger<ContactService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<Contact> DeleteContact(int id)
        {
            var contact = _db.Contacts.FirstOrDefault(c => c.Id == id);
            if (contact == null)
            {
                _logger.LogWarning("Contact with ID {ContactId} not found for deletion", id);
                return Task.FromResult<Contact>(null);
            }
            _db.Contacts.Remove(contact);
            _db.SaveChanges();
            _logger.LogInformation("Deleted Contact ID {ContactId}", id);
            return Task.FromResult(contact);
        }

        public Task<IEnumerable<Contact>> GetAllContactsAsync()
        {
            return Task.FromResult<IEnumerable<Contact>>(_db.Contacts.ToList());
        }
        public async Task SendContactAsync(ContactFormData data, string userId)
        {
            var contact = new Contact
            {
                Name = data.Name,
                Email = data.Email,
                Message = data.Message,
                UserId = userId
            };

            _db.Contacts.Add(contact);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Saved Contact ID {ContactId} for User {UserId}", contact.Id, userId);
        }
    }
}
