using Ezz_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ezz_api.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardStatistics> GetDashboardStatisticsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var startOfWeek = now.AddDays(-(int)now.DayOfWeek);

                var totalProducts = await _db.Products.CountAsync();
                var totalCategories = await _db.Categories.CountAsync();
                var totalUsers = await _db.Users.CountAsync();
                var totalOrders = await _db.Orders.CountAsync();
                
                // Fix SQLite decimal aggregation by converting to double
                var totalRevenue = await _db.Orders
                    .Where(o => o.PaymentStatus == "paid")
                    .Select(o => (double)o.TotalAmount)
                    .SumAsync();

                var pendingOrders = await _db.Orders.CountAsync(o => o.OrderStatus == "pending");
                var shippedOrders = await _db.Orders.CountAsync(o => o.OrderStatus == "shipped");
                var deliveredOrders = await _db.Orders.CountAsync(o => o.OrderStatus == "delivered");
                var cancelledOrders = await _db.Orders.CountAsync(o => o.OrderStatus == "cancelled");

                // Fix SQLite decimal aggregation for monthly revenue
                var monthlyRevenue = await _db.Orders
                    .Where(o => o.PaymentStatus == "paid" && o.CreatedAt >= startOfMonth)
                    .Select(o => (double)o.TotalAmount)
                    .SumAsync();

                // Fix SQLite decimal aggregation for weekly revenue
                var weeklyRevenue = await _db.Orders
                    .Where(o => o.PaymentStatus == "paid" && o.CreatedAt >= startOfWeek)
                    .Select(o => (double)o.TotalAmount)
                    .SumAsync();

                var recentOrders = await _db.Orders
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                var revenueChart = await GetRevenueChartDataAsync();
                var orderStatusChart = await GetOrderStatusChartDataAsync();
                var topProducts = await GetTopProductsAsync();

                return new DashboardStatistics
                {
                    TotalProducts = totalProducts,
                    TotalCategories = totalCategories,
                    TotalUsers = totalUsers,
                    TotalOrders = totalOrders,
                    TotalRevenue = (decimal)totalRevenue,
                    PendingOrders = pendingOrders,
                    ShippedOrders = shippedOrders,
                    DeliveredOrders = deliveredOrders,
                    CancelledOrders = cancelledOrders,
                    MonthlyRevenue = (decimal)monthlyRevenue,
                    WeeklyRevenue = (decimal)weeklyRevenue,
                    RecentOrders = recentOrders,
                    RevenueChart = revenueChart,
                    OrderStatusChart = orderStatusChart,
                    TopProducts = topProducts
                };
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetDashboardStatisticsAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<DashboardStatistics> GetDashboardStatisticsByDateRangeAsync(DateRange dateRange)
        {
            try
            {
                var totalProducts = await _db.Products.CountAsync();
                var totalCategories = await _db.Categories.CountAsync();
                var totalUsers = await _db.Users.CountAsync();
                
                var ordersInRange = _db.Orders
                    .Where(o => o.CreatedAt >= dateRange.StartDate && o.CreatedAt <= dateRange.EndDate);

                var totalOrders = await ordersInRange.CountAsync();
                
                // Fix SQLite decimal aggregation
                var totalRevenue = await ordersInRange
                    .Where(o => o.PaymentStatus == "paid")
                    .Select(o => (double)o.TotalAmount)
                    .SumAsync();

                var pendingOrders = await ordersInRange.CountAsync(o => o.OrderStatus == "pending");
                var shippedOrders = await ordersInRange.CountAsync(o => o.OrderStatus == "shipped");
                var deliveredOrders = await ordersInRange.CountAsync(o => o.OrderStatus == "delivered");
                var cancelledOrders = await ordersInRange.CountAsync(o => o.OrderStatus == "cancelled");

                var recentOrders = await ordersInRange
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                return new DashboardStatistics
                {
                    TotalProducts = totalProducts,
                    TotalCategories = totalCategories,
                    TotalUsers = totalUsers,
                    TotalOrders = totalOrders,
                    TotalRevenue = (decimal)totalRevenue,
                    PendingOrders = pendingOrders,
                    ShippedOrders = shippedOrders,
                    DeliveredOrders = deliveredOrders,
                    CancelledOrders = cancelledOrders,
                    RecentOrders = recentOrders
                };
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetDashboardStatisticsByDateRangeAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<List<RevenueChartData>> GetRevenueChartDataAsync(int days = 30)
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-days);

                // Get the data first, then process on client side
                var orders = await _db.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.PaymentStatus == "paid")
                    .Select(o => new { o.CreatedAt, o.TotalAmount })
                    .ToListAsync();

                // Process on client side to avoid LINQ translation issues
                var dailyRevenue = orders
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new RevenueChartData
                    {
                        Period = g.Key.ToString("MMM dd"),
                        Revenue = (decimal)g.Sum(o => (double)o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(r => r.Period)
                    .ToList();

                return dailyRevenue;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetRevenueChartDataAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<List<OrderStatusChartData>> GetOrderStatusChartDataAsync()
        {
            try
            {
                // Get the data first, then process on client side to avoid LINQ translation issues
                var orders = await _db.Orders
                    .Select(o => new { o.OrderStatus })
                    .ToListAsync();

                var statusCounts = orders
                    .GroupBy(o => o.OrderStatus)
                    .Select(g => new OrderStatusChartData
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        Color = GetStatusColor(g.Key)
                    })
                    .ToList();

                return statusCounts;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetOrderStatusChartDataAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TopProductData>> GetTopProductsAsync(int count = 5)
        {
            try
            {
                // Get the data first, then process on client side
                var orderItems = await _db.OrderItems
                    .Select(oi => new { oi.ProductId, oi.ProductName, oi.Quantity, oi.TotalPrice })
                    .ToListAsync();

                var topProducts = orderItems
                    .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                    .Select(g => new TopProductData
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        TotalSold = g.Sum(oi => oi.Quantity),
                        TotalRevenue = (decimal)g.Sum(oi => (double)oi.TotalPrice)
                    })
                    .OrderByDescending(p => p.TotalSold)
                    .Take(count)
                    .ToList();

                // Get image URLs for top products
                foreach (var product in topProducts)
                {
                    var productImage = await _db.ProductImages
                        .Where(pi => pi.ProductId == product.ProductId)
                        .FirstOrDefaultAsync();
                    
                    if (productImage != null)
                    {
                        product.ImageUrl = productImage.ImageUrl;
                    }
                }

                return topProducts;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error in GetTopProductsAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "pending" => "#FFA500",
                "confirmed" => "#007BFF",
                "shipped" => "#17A2B8",
                "delivered" => "#28A745",
                "cancelled" => "#DC3545",
                _ => "#6C757D"
            };
        }
    }
} 