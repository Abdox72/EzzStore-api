namespace Ezz_api.Models
{
    public class DashboardStatistics
    {
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public List<Order> RecentOrders { get; set; } = new List<Order>();
        public List<RevenueChartData> RevenueChart { get; set; } = new List<RevenueChartData>();
        public List<OrderStatusChartData> OrderStatusChart { get; set; } = new List<OrderStatusChartData>();
        public List<TopProductData> TopProducts { get; set; } = new List<TopProductData>();
    }

    public class RevenueChartData
    {
        public string Period { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class OrderStatusChartData
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class TopProductData
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class DateRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
} 