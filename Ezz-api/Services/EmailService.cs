using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace Ezz_api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpHost = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:Port"] ?? "587");
            _smtpUsername = _configuration["Email:Username"] ?? "";
            _smtpPassword = _configuration["Email:Password"] ?? "";
            _fromEmail = _configuration["Email:From"] ?? "noreply@ezz.com";
            _fromName = _configuration["Email:FromName"] ?? "Ezz Store";
        }


        public async Task SendOrderStatusUpdateAsync(string toEmail, string customerName, string orderNumber, string newStatus, string? trackingNumber = null, string? carrier = null)
        {
            var subject = $"تحديث حالة الطلب #{orderNumber}";
            var body = GenerateOrderStatusUpdateBody(customerName, orderNumber, newStatus, trackingNumber, carrier);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendOrderCancellationAsync(string toEmail, string customerName, string orderNumber, string reason)
        {
            var subject = $"إلغاء الطلب #{orderNumber}";
            var body = GenerateOrderCancellationBody(customerName, orderNumber, reason);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendOrderRefundAsync(string toEmail, string customerName, string orderNumber, string reason)
        {
            var subject = $"استرداد الطلب #{orderNumber}";
            var body = GenerateOrderRefundBody(customerName, orderNumber, reason);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendOrderShippedAsync(string toEmail, string customerName, string orderNumber, string trackingNumber, string carrier)
        {
            var subject = $"تم شحن الطلب #{orderNumber}";
            var body = GenerateOrderShippedBody(customerName, orderNumber, trackingNumber, carrier);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendOrderDeliveredAsync(string toEmail, string customerName, string orderNumber)
        {
            var subject = $"تم توصيل الطلب #{orderNumber}";
            var body = GenerateOrderDeliveredBody(customerName, orderNumber);
            await SendEmailAsync(toEmail, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_fromName, _fromEmail));
                email.To.Add(new MailboxAddress("", toEmail));
                email.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                email.Body = bodyBuilder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_smtpUsername, _smtpPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to avoid breaking the order flow
                Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
            }
        }

        private string GenerateOrderStatusUpdateBody(string customerName, string orderNumber, string newStatus, string? trackingNumber, string? carrier)
        {
            var statusText = GetStatusText(newStatus);
            var trackingInfo = trackingNumber != null ? $"<p><strong>رقم التتبع:</strong> {trackingNumber}</p><p><strong>شركة الشحن:</strong> {carrier}</p>" : "";

            return $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #333;'>مرحباً {customerName}</h2>
                    <p>تم تحديث حالة طلبك #{orderNumber} إلى: <strong>{statusText}</strong></p>
                    {trackingInfo}
                    <p>شكراً لك على اختيار متجر Ezz</p>
                    <p>مع تحيات،<br>فريق Ezz</p>
                </div>";
        }

        private string GenerateOrderCancellationBody(string customerName, string orderNumber, string reason)
        {
            return $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #d32f2f;'>مرحباً {customerName}</h2>
                    <p>تم إلغاء طلبك #{orderNumber}</p>
                    <p><strong>سبب الإلغاء:</strong> {reason}</p>
                    <p>إذا كان لديك أي استفسارات، يرجى التواصل معنا</p>
                    <p>شكراً لك على اختيار متجر Ezz</p>
                    <p>مع تحيات،<br>فريق Ezz</p>
                </div>";
        }

        private string GenerateOrderRefundBody(string customerName, string orderNumber, string reason)
        {
            return $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #1976d2;'>مرحباً {customerName}</h2>
                    <p>تم استرداد طلبك #{orderNumber}</p>
                    <p><strong>سبب الاسترداد:</strong> {reason}</p>
                    <p>سيتم إرجاع المبلغ إلى حسابك خلال 3-5 أيام عمل</p>
                    <p>شكراً لك على اختيار متجر Ezz</p>
                    <p>مع تحيات،<br>فريق Ezz</p>
                </div>";
        }

        private string GenerateOrderShippedBody(string customerName, string orderNumber, string trackingNumber, string carrier)
        {
            return $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #388e3c;'>مرحباً {customerName}</h2>
                    <p>تم شحن طلبك #{orderNumber} بنجاح!</p>
                    <p><strong>رقم التتبع:</strong> {trackingNumber}</p>
                    <p><strong>شركة الشحن:</strong> {carrier}</p>
                    <p>يمكنك تتبع طلبك من خلال الرابط التالي:</p>
                    <p><a href='https://tracking.{carrier.ToLower()}.com/{trackingNumber}' style='color: #1976d2;'>تتبع الطلب</a></p>
                    <p>شكراً لك على اختيار متجر Ezz</p>
                    <p>مع تحيات،<br>فريق Ezz</p>
                </div>";
        }

        private string GenerateOrderDeliveredBody(string customerName, string orderNumber)
        {
            return $@"
                <div dir='rtl' style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #388e3c;'>مرحباً {customerName}</h2>
                    <p>تم توصيل طلبك #{orderNumber} بنجاح!</p>
                    <p>نأمل أن تكون راضياً عن منتجاتنا</p>
                    <p>إذا كان لديك أي استفسارات أو تريد إرجاع المنتج، يرجى التواصل معنا</p>
                    <p>شكراً لك على اختيار متجر Ezz</p>
                    <p>مع تحيات،<br>فريق Ezz</p>
                </div>";
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "pending" => "قيد الانتظار",
                "confirmed" => "مؤكد",
                "shipped" => "تم الشحن",
                "delivered" => "تم التوصيل",
                "cancelled" => "ملغي",
                _ => status
            };
        }
    }
}
