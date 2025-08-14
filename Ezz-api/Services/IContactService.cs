using Ezz_api.Models;
using Ezz_api.ViewModel;

namespace Ezz_api.Services
{
    public interface IContactService
    {
        /// <summary>
        /// Saves or processes a contact form submission for the given user.
        /// </summary>
        Task SendContactAsync(ContactFormData data, string userId);

        /// <summary>
        /// get all contacts for admin to see
        /// </summary>
        Task<IEnumerable<Contact>> GetAllContactsAsync();
        /// <summary>
        /// delete a contact message by id
        /// </summary>
        Task<Contact> DeleteContact(int id);

    }
}
