using Ezz_api.Models;

namespace Ezz_api.Services
{
    public interface IDashboardService
    {
        Task<DashboardStatistics> GetDashboardStatisticsAsync();
        Task<DashboardStatistics> GetDashboardStatisticsByDateRangeAsync(DateRange dateRange);
        Task<List<RevenueChartData>> GetRevenueChartDataAsync(int days = 30);
        Task<List<OrderStatusChartData>> GetOrderStatusChartDataAsync();
        Task<List<TopProductData>> GetTopProductsAsync(int count = 5);
    }
} 