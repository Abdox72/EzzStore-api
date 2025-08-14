namespace Ezz_api.Services
{
    public interface IPayPalService
    {
        Task<PayPalOrderResponse> CreateOrderAsync(PayPalOrderRequest request);
        Task<PayPalCaptureResponse> CaptureOrderAsync(string orderId);
        Task<PayPalRefundResponse> RefundPaymentAsync(string captureId, decimal amount, string reason);
        Task<PayPalOrderDetails> GetOrderDetailsAsync(string orderId);
    }

    public class PayPalOrderRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class PayPalOrderResponse
    {
        public bool Success { get; set; }
        public string? OrderId { get; set; }
        public string? ApprovalUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PayPalCaptureResponse
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PayPalRefundResponse
    {
        public bool Success { get; set; }
        public string? RefundId { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PayPalOrderDetails
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
