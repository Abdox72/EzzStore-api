using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Ezz_api.Services
{
    public class PayPalService : IPayPalService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private string? _accessToken;

        public PayPalService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _clientId = _configuration["PayPal:ClientId"] ?? "";
            _clientSecret = _configuration["PayPal:ClientSecret"] ?? "";
            _baseUrl = _configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
        }

        public async Task<PayPalOrderResponse> CreateOrderAsync(PayPalOrderRequest request)
        {
            try
            {
                await EnsureAccessTokenAsync();

                var paypalRequest = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            amount = new
                            {
                                currency_code = request.Currency,
                                value = request.Amount.ToString("F2")
                            },
                            description = request.Description,
                            custom_id = request.CustomerEmail
                        }
                    },
                    application_context = new
                    {
                        return_url = request.ReturnUrl,
                        cancel_url = request.CancelUrl,
                        brand_name = "Ezz Store",
                        landing_page = "LOGIN",
                        user_action = "PAY_NOW"
                    }
                };

                var json = JsonSerializer.Serialize(paypalRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/v2/checkout/orders", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var paypalResponse = JsonSerializer.Deserialize<PayPalCreateOrderResponse>(responseContent);
                    if (paypalResponse?.id != null)
                    {
                        var approvalUrl = paypalResponse.links?.FirstOrDefault(l => l.rel == "approve")?.href;
                        return new PayPalOrderResponse
                        {
                            Success = true,
                            OrderId = paypalResponse.id,
                            ApprovalUrl = approvalUrl
                        };
                    }
                }

                return new PayPalOrderResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create PayPal order: {responseContent}"
                };
            }
            catch (Exception ex)
            {
                return new PayPalOrderResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PayPalCaptureResponse> CaptureOrderAsync(string orderId)
        {
            try
            {
                await EnsureAccessTokenAsync();

                // minimal JSON body so Content-Type is set correctly
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                // Option A: simple PostAsync
                var response = await _httpClient.PostAsync($"{_baseUrl}/v2/checkout/orders/{orderId}/capture", content);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var captureResponse = JsonSerializer.Deserialize<PayPalCaptureOrderResponse>(responseContent);
                    if (captureResponse?.purchase_units?.FirstOrDefault()?.payments?.captures?.FirstOrDefault() is var capture)
                    {
                        return new PayPalCaptureResponse
                        {
                            Success = true,
                            TransactionId = capture.id,
                            Status = capture.status
                        };
                    }
                }

                return new PayPalCaptureResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to capture PayPal order: {responseContent}"
                };
            }
            catch (Exception ex)
            {
                return new PayPalCaptureResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PayPalRefundResponse> RefundPaymentAsync(string captureId, decimal amount, string reason)
        {
            try
            {
                await EnsureAccessTokenAsync();

                var refundRequest = new
                {
                    amount = new
                    {
                        currency_code = "USD",
                        value = amount.ToString("F2")
                    },
                    note_to_payer = reason
                };

                var json = JsonSerializer.Serialize(refundRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/v2/payments/captures/{captureId}/refund", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;
                    var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : string.Empty;
                    var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : string.Empty;
                    return new PayPalRefundResponse
                    {
                        Success = true,
                        RefundId = id,
                        Status = status
                    };
                }

                return new PayPalRefundResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to refund PayPal payment: {responseContent}"
                };
            }
            catch (Exception ex)
            {
                return new PayPalRefundResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PayPalOrderDetails> GetOrderDetailsAsync(string orderId)
        {
            try
            {
                await EnsureAccessTokenAsync();

                var response = await _httpClient.GetAsync($"{_baseUrl}/v2/checkout/orders/{orderId}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var orderResponse = JsonSerializer.Deserialize<PayPalOrderDetailsResponse>(responseContent);
                    if (orderResponse != null)
                    {
                        var purchaseUnit = orderResponse.purchase_units?.FirstOrDefault();
                        return new PayPalOrderDetails
                        {
                            OrderId = orderResponse.id,
                            Status = orderResponse.status,
                            Amount = purchaseUnit?.amount?.value != null ? decimal.Parse(purchaseUnit.amount.value) : 0,
                            Currency = purchaseUnit?.amount?.currency_code ?? "USD",
                            CreatedAt = orderResponse.create_time != null ? DateTime.Parse(orderResponse.create_time) : DateTime.UtcNow,
                            UpdatedAt = orderResponse.update_time != null ? DateTime.Parse(orderResponse.update_time) : null
                        };
                    }
                }

                throw new Exception($"Failed to get PayPal order details: {responseContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting PayPal order details: {ex.Message}");
            }
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken))
                return;

            var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/oauth2/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var authResponse = JsonSerializer.Deserialize<PayPalAuthResponse>(responseContent);
                _accessToken = authResponse?.access_token;
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }
            else
            {
                throw new Exception($"Failed to get PayPal access token: {responseContent}");
            }
        }


        // PayPal API response models
        private class PayPalAuthResponse
        {
            public string? access_token { get; set; }
            public string? token_type { get; set; }
            public int expires_in { get; set; }
        }

        private class PayPalCreateOrderResponse
        {
            public string? id { get; set; }
            public string? status { get; set; }
            public List<PayPalLink>? links { get; set; }
        }

        private class PayPalLink
        {
            public string? href { get; set; }
            public string? rel { get; set; }
            public string? method { get; set; }
        }

        private class PayPalCaptureOrderResponse
        {
            public string? id { get; set; }
            public string? status { get; set; }
            public List<PayPalPurchaseUnit>? purchase_units { get; set; }
        }

        private class PayPalPurchaseUnit
        {
            public PayPalPayments? payments { get; set; }
            public PayPalAmount? amount { get; set; }
        }

        private class PayPalPayments
        {
            public List<PayPalCapture>? captures { get; set; }
        }

        private class PayPalCapture
        {
            public string? id { get; set; }
            public string? status { get; set; }
            public PayPalAmount? amount { get; set; }
        }

        private class PayPalAmount
        {
            public string? currency_code { get; set; }
            public string? value { get; set; }
        }

        private class PayPalOrderDetailsResponse
        {
            public string? id { get; set; }
            public string? status { get; set; }
            public string? create_time { get; set; }
            public string? update_time { get; set; }
            public List<PayPalPurchaseUnit>? purchase_units { get; set; }
        }
        // Removed duplicate PayPalRefundResponse class definition
    }
}
