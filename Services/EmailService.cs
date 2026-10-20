using System.Net;
using EStore.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EStore.Services
{
    // Reads the "Smtp" section from appsettings.json and User Secrets
    public class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromName { get; set; } = "Ever Spring";
        public string FromEmail { get; set; } = "";
        public string? AdminNotifyEmail { get; set; }
    }

    public interface IEmailService
    {
        Task<bool> SendAsync(string to, string subject, string htmlBody);
        Task SendOrderConfirmationAsync(Order order);
        Task SendOrderStatusAsync(Order order);
        Task SendWelcomeAsync(string to, string name);
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _s;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<SmtpSettings> options,
            ILogger<EmailService> logger)
        {
            _s = options.Value;
            _logger = logger;
        }

        // Returns true if sent. Never throws, so an email problem can't break checkout.
        public async Task<bool> SendAsync(string to, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(_s.Host) ||
                string.IsNullOrWhiteSpace(_s.User) ||
                string.IsNullOrWhiteSpace(_s.Password))
            {
                _logger.LogWarning("Email not sent: Smtp settings are missing.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(to)) return false;

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_s.FromName, _s.FromEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_s.Host, _s.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_s.User, _s.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                return false;
            }
        }

        // ---------- Order confirmation (customer + admin) ----------
        public async Task SendOrderConfirmationAsync(Order o)
        {
            var rows = string.Join("", o.OrderItems.Select(i =>
                "<tr>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee'>{H(i.ProductName)} × {i.Quantity}</td>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right'>৳{i.TotalPrice:N0}</td>" +
                "</tr>"));

            var paymentLine = o.PaymentStatus == "Paid"
                ? "Payment received. Thank you!"
                : "Payment method: Cash on Delivery.";

            var body = Wrap("Order Confirmed",
                $"<p>Hi {H(o.CustomerName)},</p>" +
                $"<p>Thank you for shopping with us. Your order <strong>{H(o.OrderNumber)}</strong> has been received.</p>" +
                $"<table style='width:100%;border-collapse:collapse'>{rows}" +
                "<tr><td style='padding:8px'><strong>Total</strong></td>" +
                $"<td style='padding:8px;text-align:right'><strong>৳{o.TotalAmount:N0}</strong></td></tr></table>" +
                $"<p>{paymentLine}</p>" +
                $"<p><strong>Shipping to:</strong><br>{H(o.ShippingAddress)}, {H(o.City)} {H(o.PostCode)}<br>{H(o.CustomerPhone)}</p>");

            await SendAsync(o.CustomerEmail, $"Order Confirmed – {o.OrderNumber}", body);

            if (!string.IsNullOrWhiteSpace(_s.AdminNotifyEmail))
            {
                await SendAsync(
                    _s.AdminNotifyEmail,
                    $"New order {o.OrderNumber} – ৳{o.TotalAmount:N0}",
                    Wrap("New Order",
                        $"<p>Customer: {H(o.CustomerName)} ({H(o.CustomerPhone)})</p>" +
                        $"<p>Total: ৳{o.TotalAmount:N0}</p>" +
                        $"<p>Payment: {H(o.PaymentMethod)} ({H(o.PaymentStatus)})</p>"));
            }
        }

        // ---------- Order status update ----------
        public async Task SendOrderStatusAsync(Order o)
        {
            var message = o.OrderStatus switch
            {
                "Confirmed" => "Your order has been confirmed and is being prepared.",
                "Processing" => "Your order is being processed.",
                "Shipped" => "Good news! Your order has been shipped.",
                "Delivered" => "Your order has been delivered. We hope you love it!",
                "Cancelled" => "Your order has been cancelled. Please contact us if you have any questions.",
                _ => null
            };

            if (message == null) return;

            await SendAsync(
                o.CustomerEmail,
                $"Order {o.OrderNumber} – {o.OrderStatus}",
                Wrap($"Order {o.OrderStatus}",
                    $"<p>Hi {H(o.CustomerName)},</p>" +
                    $"<p>{message}</p>" +
                    $"<p>Order number: <strong>{H(o.OrderNumber)}</strong></p>"));
        }

        // ---------- Welcome email ----------
        public Task SendWelcomeAsync(string to, string name) =>
            SendAsync(to, "Welcome to Ever Spring",
                Wrap("Welcome!",
                    $"<p>Hi {H(name)},</p><p>Your account has been created. Happy shopping!</p>"));

        // ---------- Helpers ----------

        // Customer names are user input, so HTML-encode them to prevent HTML injection
        private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

        private string Wrap(string title, string content) =>
            "<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;border:1px solid #eee'>" +
            $"<div style='background:#111;color:#fff;padding:16px 24px'><h2 style='margin:0'>{H(_s.FromName)}</h2></div>" +
            $"<div style='padding:24px'><h3>{title}</h3>{content}</div>" +
            "<div style='padding:12px 24px;background:#fafafa;color:#888;font-size:12px'>This is an automated email.</div>" +
            "</div>";
    }
}