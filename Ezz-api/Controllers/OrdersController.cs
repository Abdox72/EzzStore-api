using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Ezz_api.Models;
using Ezz_api.Services;
using System.Text.Json.Serialization;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IPayPalService _payPalService;

        public OrdersController(ApplicationDbContext db, IEmailService emailService, IPayPalService payPalService)
        {
            _db = db;
            _emailService = emailService;
            _payPalService = payPalService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check inventory availability
                foreach (var item in request.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product == null)
                        return BadRequest($"Product with ID {item.ProductId} not found");
                    
                    if (product.Stock < item.Quantity)
                        return BadRequest($"Insufficient stock for product {product.Title}. Available: {product.Stock}");
                }

                var order = new Order
                {
                    UserId = userId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    CustomerAddress = request.CustomerAddress,
                    CustomerCity = request.CustomerCity,
                    CustomerPostalCode = request.CustomerPostalCode,
                    TotalAmount = request.TotalAmount,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = request.PaymentMethod == "stripe" || request.PaymentMethod == "paypal" ? "pending" : "pending",
                    OrderStatus = "pending",
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = new List<OrderItem>() // ✅ Initialize this
                };

                // Add order items and update inventory
                foreach (var item in request.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product == null)
                        return BadRequest($"Product with ID {item.ProductId} not found");

                    // Reduce inventory
                    product.Stock -= item.Quantity;

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = product.Title,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price,
                        TotalPrice = product.Price * item.Quantity
                    });
                }

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Send confirmation email
                await _emailService.SendOrderStatusUpdateAsync(
                    order.CustomerEmail, 
                    order.CustomerName, 
                    order.Id.ToString(), 
                    order.OrderStatus
                );

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                return NotFound();

            return Ok(order);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserOrders()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var orders = await _db.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders([FromQuery] string? status, [FromQuery] string? paymentStatus)
        {
            var query = _db.Orders.Include(o => o.OrderItems).AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.OrderStatus == status);

            if (!string.IsNullOrEmpty(paymentStatus))
                query = query.Where(o => o.PaymentStatus == paymentStatus);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("admin/paginated")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaginatedOrders([FromQuery] OrderFilterParameters parameters)
        {
            try
            {
                var query = _db.Orders.Include(o => o.OrderItems).AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(parameters.Status))
                    query = query.Where(o => o.OrderStatus == parameters.Status);

                if (!string.IsNullOrEmpty(parameters.PaymentStatus))
                    query = query.Where(o => o.PaymentStatus == parameters.PaymentStatus);

                if (!string.IsNullOrEmpty(parameters.PaymentMethod))
                    query = query.Where(o => o.PaymentMethod == parameters.PaymentMethod);

                if (parameters.StartDate.HasValue)
                    query = query.Where(o => o.CreatedAt >= parameters.StartDate.Value);

                if (parameters.EndDate.HasValue)
                    query = query.Where(o => o.CreatedAt <= parameters.EndDate.Value);

                if (parameters.MinAmount.HasValue)
                    query = query.Where(o => o.TotalAmount >= parameters.MinAmount.Value);

                if (parameters.MaxAmount.HasValue)
                    query = query.Where(o => o.TotalAmount <= parameters.MaxAmount.Value);

                if (!string.IsNullOrEmpty(parameters.CustomerName))
                    query = query.Where(o => o.CustomerName.Contains(parameters.CustomerName));

                if (!string.IsNullOrEmpty(parameters.CustomerEmail))
                    query = query.Where(o => o.CustomerEmail.Contains(parameters.CustomerEmail));

                if (!string.IsNullOrEmpty(parameters.SearchTerm))
                {
                    var searchTerm = parameters.SearchTerm.ToLower();
                    query = query.Where(o => 
                        o.CustomerName.ToLower().Contains(searchTerm) ||
                        o.CustomerEmail.ToLower().Contains(searchTerm) ||
                        o.Id.ToString().Contains(searchTerm) ||
                        o.TrackingNumber != null && o.TrackingNumber.ToLower().Contains(searchTerm)
                    );
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                if (!string.IsNullOrEmpty(parameters.SortBy))
                {
                    query = parameters.SortBy.ToLower() switch
                    {
                        "amount" => parameters.SortDescending ? query.OrderByDescending(o => o.TotalAmount) : query.OrderBy(o => o.TotalAmount),
                        "date" => parameters.SortDescending ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt),
                        "customer" => parameters.SortDescending ? query.OrderByDescending(o => o.CustomerName) : query.OrderBy(o => o.CustomerName),
                        "status" => parameters.SortDescending ? query.OrderByDescending(o => o.OrderStatus) : query.OrderBy(o => o.OrderStatus),
                        _ => parameters.SortDescending ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt)
                    };
                }
                else
                {
                    query = query.OrderByDescending(o => o.CreatedAt);
                }

                // Apply pagination
                var orders = await query
                    .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                    .Take(parameters.PageSize)
                    .ToListAsync();

                var response = new PaginatedResponse<Order>(orders, totalCount, parameters.PageNumber, parameters.PageSize);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
                return NotFound();

            var oldStatus = order.OrderStatus;
            order.OrderStatus = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            // Set specific timestamps based on status
            switch (request.Status)
            {
                case "shipped":
                    order.ShippedAt = DateTime.UtcNow;
                    break;
                case "delivered":
                    order.DeliveredAt = DateTime.UtcNow;
                    break;
            }

            await _db.SaveChangesAsync();

            // Send email notification
            await _emailService.SendOrderStatusUpdateAsync(
                order.CustomerEmail,
                order.CustomerName,
                order.Id.ToString(),
                request.Status,
                order.TrackingNumber,
                order.Carrier
            );

            // Special handling for shipped status
            if (request.Status == "shipped" && !string.IsNullOrEmpty(order.TrackingNumber))
            {
                await _emailService.SendOrderShippedAsync(
                    order.CustomerEmail,
                    order.CustomerName,
                    order.Id.ToString(),
                    order.TrackingNumber,
                    order.Carrier ?? "Unknown"
                );
            }

            // Special handling for delivered status
            if (request.Status == "delivered")
            {
                await _emailService.SendOrderDeliveredAsync(
                    order.CustomerEmail,
                    order.CustomerName,
                    order.Id.ToString()
                );
            }

            return Ok(order);
        }

        [HttpPut("{id:int}/payment-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
                return NotFound();

            order.PaymentStatus = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(order);
        }

        [HttpPut("{id:int}/tracking")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTrackingInfo(int id, [FromBody] UpdateTrackingRequest request)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
                return NotFound();

            order.TrackingNumber = request.TrackingNumber;
            order.Carrier = request.Carrier;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(order);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                return NotFound();

            if (order.OrderStatus != "pending")
                return BadRequest("Only pending orders can be cancelled");

            order.OrderStatus = "cancelled";
            order.IsCancelled = true;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = request.Reason;
            order.UpdatedAt = DateTime.UtcNow;

            // Restore inventory
            foreach (var item in order.OrderItems)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Stock += item.Quantity;
                }
            }

            await _db.SaveChangesAsync();

            // Send cancellation email
            await _emailService.SendOrderCancellationAsync(
                order.CustomerEmail,
                order.CustomerName,
                order.Id.ToString(),
                request.Reason
            );

            return Ok(order);
        }

        [HttpPost("{id:int}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RefundOrder(int id, [FromBody] RefundOrderRequest request)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            if (order.PaymentStatus != "paid")
                return BadRequest("Only paid orders can be refunded");

            order.PaymentStatus = "refunded";
            order.IsRefunded = true;
            order.RefundedAt = DateTime.UtcNow;
            order.RefundReason = request.Reason;
            order.UpdatedAt = DateTime.UtcNow;

            // Restore inventory
            foreach (var item in order.OrderItems)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Stock += item.Quantity;
                }
            }

            // Process PayPal refund if applicable
            if (order.PaymentMethod == "paypal" && !string.IsNullOrEmpty(order.PayPalTransactionId))
            {
                try
                {
                    var refundResult = await _payPalService.RefundPaymentAsync(
                        order.PayPalTransactionId,
                        order.TotalAmount,
                        request.Reason
                    );

                    if (!refundResult.Success)
                    {
                        return BadRequest($"PayPal refund failed: {refundResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    return BadRequest($"PayPal refund error: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync();

            // Send refund email
            await _emailService.SendOrderRefundAsync(
                order.CustomerEmail,
                order.CustomerName,
                order.Id.ToString(),
                request.Reason
            );

            return Ok(order);
        }

        [HttpPost("paypal/create")]
        public async Task<IActionResult> CreatePayPalOrder([FromBody] CreatePayPalOrderRequest request)
        {
            try
            {
                var paypalResult = await _payPalService.CreateOrderAsync(new PayPalOrderRequest
                {
                    Amount = request.Amount,
                    Currency = "USD",
                    Description = request.Description,
                    CustomerEmail = request.CustomerEmail,
                    ReturnUrl = request.ReturnUrl,
                    CancelUrl = request.CancelUrl
                });

                if (paypalResult.Success)
                {
                    return Ok(paypalResult);
                }

                return BadRequest(paypalResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("paypal/capture")]
        public async Task<IActionResult> CapturePayPalOrder([FromBody] CapturePayPalOrderRequest request)
        {
            try
            {
                var captureResult = await _payPalService.CaptureOrderAsync(request.OrderId);
                if (captureResult.Success)
                {
                    return Ok(captureResult);
                }
                return BadRequest(captureResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class CreateOrderRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerAddress { get; set; }
        public string? CustomerCity { get; set; }
        public string? CustomerPostalCode { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdatePaymentStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdateTrackingRequest
    {
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RefundOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class CreatePayPalOrderRequest
    {
        // is totalAmount from the json
        [JsonPropertyName("totalAmount")]
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class CapturePayPalOrderRequest
    {
        public string OrderId { get; set; } = string.Empty;
    }
} 