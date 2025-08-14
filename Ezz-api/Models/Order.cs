using System.ComponentModel.DataAnnotations;

namespace Ezz_api.Models
{
    public class Order
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string CustomerEmail { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? CustomerAddress { get; set; }
        
        [MaxLength(50)]
        public string? CustomerCity { get; set; }
        
        [MaxLength(10)]
        public string? CustomerPostalCode { get; set; }
        
        [Required]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string PaymentMethod { get; set; } = string.Empty; // "whatsapp", "stripe", or "paypal"
        
        [Required]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = string.Empty; // "pending", "paid", "failed", "refunded"
        
        [Required]
        [MaxLength(20)]
        public string OrderStatus { get; set; } = string.Empty; // "pending", "confirmed", "shipped", "delivered", "cancelled"
        
        // Tracking information
        [MaxLength(100)]
        public string? TrackingNumber { get; set; }
        
        [MaxLength(100)]
        public string? Carrier { get; set; }
        
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        
        // Cancellation and refund information
        public bool IsCancelled { get; set; } = false;
        public DateTime? CancelledAt { get; set; }
        [MaxLength(500)]
        public string? CancellationReason { get; set; }
        
        public bool IsRefunded { get; set; } = false;
        public DateTime? RefundedAt { get; set; }
        [MaxLength(500)]
        public string? RefundReason { get; set; }
        
        // PayPal specific fields
        [MaxLength(100)]
        public string? PayPalOrderId { get; set; }
        [MaxLength(100)]
        public string? PayPalTransactionId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ApplicationUser User { get; set; } = null!;
    }
} 