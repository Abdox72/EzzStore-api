using Ezz_api.DTOs;

namespace Ezz_api.Services
{
    public interface IChatbotService
    {
        // Process a message with optional conversation history and user context
        Task<ChatbotResponse> ProcessMessageAsync(ChatbotRequest request);
        
        // Get suggested products based on user query or conversation context
        Task<List<Models.Product>> GetSuggestedProductsAsync(string query, int maxResults = 5);
        
        // Get relevant categories based on user query
        Task<List<Models.Category>> GetRelevantCategoriesAsync(string query, int maxResults = 3);
    }
}