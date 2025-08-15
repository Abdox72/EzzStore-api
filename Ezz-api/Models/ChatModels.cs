using System.ComponentModel.DataAnnotations;

namespace Ezz_api.Models
{
    public class ChatRequest
    {
        [Required(ErrorMessage = "نص السؤال مطلوب")]
        public string Question { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string QueryType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Data { get; set; }
    }

    public class ProductSalesData
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class ProductPriceData
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class CategoryProductData
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public decimal TotalValue { get; set; }
    }
}

