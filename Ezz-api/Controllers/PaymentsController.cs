using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace Ezz_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PaymentsController(IConfiguration configuration)
        {
            _configuration = configuration;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = request.Amount,
                    Currency = request.Currency ?? "usd",
                    Description = request.Description,
                    ReceiptEmail = request.CustomerEmail,
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_email", request.CustomerEmail },
                        { "user_id", User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "" }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                return Ok(new
                {
                    clientSecret = paymentIntent.ClientSecret,
                    amount = paymentIntent.Amount
                });
            }
            catch (StripeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.ConfirmAsync(request.PaymentIntentId, new PaymentIntentConfirmOptions
                {
                    PaymentMethod = request.PaymentMethodId
                });

                if (paymentIntent.Status == "succeeded")
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Payment successful",
                        transactionId = paymentIntent.Id
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Payment failed",
                        status = paymentIntent.Status
                    });
                }
            }
            catch (StripeException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var options = new PaymentIntentListOptions
                {
                    Limit = 50
                };

                var service = new PaymentIntentService();
                var paymentIntents = await service.ListAsync(options);

                var userPayments = paymentIntents.Data
                    .Where(p => p.Metadata.ContainsKey("user_id") && p.Metadata["user_id"] == userId)
                    .Select(p => new
                    {
                        id = p.Id,
                        amount = p.Amount,
                        currency = p.Currency,
                        status = p.Status,
                        description = p.Description,
                        created = p.Created,
                        receiptEmail = p.ReceiptEmail
                    })
                    .ToList();

                return Ok(userPayments);
            }
            catch (StripeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refund")]
        public async Task<IActionResult> RefundPayment([FromBody] RefundRequest request)
        {
            try
            {
                var options = new RefundCreateOptions
                {
                    PaymentIntent = request.PaymentIntentId
                };

                if (request.Amount.HasValue)
                {
                    options.Amount = request.Amount.Value;
                }

                var service = new RefundService();
                var refund = await service.CreateAsync(options);

                return Ok(new
                {
                    success = true,
                    message = "Refund successful",
                    refundId = refund.Id
                });
            }
            catch (StripeException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }

    public class CreatePaymentIntentRequest
    {
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }

    public class ConfirmPaymentRequest
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public string PaymentMethodId { get; set; } = string.Empty;
    }

    public class RefundRequest
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public long? Amount { get; set; }
    }
} 