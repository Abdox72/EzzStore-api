using Ezz_api.Models;

namespace Ezz_api.Services
{
    public interface IChatService
    {
        Task<ChatResponse> ProcessQuestionAsync(ChatRequest request);
        Task<ChatResponse> GetTopSellingProductsAsync(string category = "");
        Task<ChatResponse> GetLowestPriceProductsAsync(string category = "");
        Task<ChatResponse> GetHighestStockProductsAsync(string category = "");
        Task<ChatResponse> GetCategoryStatisticsAsync();
        Task<ChatResponse> GetProductCountAsync();
        Task<ChatResponse> GetTotalRevenueAsync();
    }
}

