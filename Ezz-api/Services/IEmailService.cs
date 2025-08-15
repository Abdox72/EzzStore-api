namespace Ezz_api.Services
{
    public interface IEmailService
    {
        Task SendOrderStatusUpdateAsync(string toEmail, string customerName, string orderNumber, string newStatus, string? trackingNumber = null, string? carrier = null);
        Task SendOrderCancellationAsync(string toEmail, string customerName, string orderNumber, string reason);
        Task SendOrderRefundAsync(string toEmail, string customerName, string orderNumber, string reason);
        Task SendOrderShippedAsync(string toEmail, string customerName, string orderNumber, string trackingNumber, string carrier);
        Task SendOrderDeliveredAsync(string toEmail, string customerName, string orderNumber);
    }
}
