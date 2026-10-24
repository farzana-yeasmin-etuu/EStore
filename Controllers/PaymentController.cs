using System.Security.Claims;
using EStore.Data;
using EStore.Models;
using EStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EStore.Filters;

namespace EStore.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SslCommerzService _ssl;
        private readonly OrderService _orders;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext db,
            SslCommerzService ssl,
            OrderService orders,
            IEmailService email,
            IConfiguration config,
            ILogger<PaymentController> logger)
        {
            _db = db;
            _ssl = ssl;
            _orders = orders;
            _email = email;
            _config = config;
            _logger = logger;
        }

        // =====================================================
        // PAY: send the logged-in customer to the SSLCommerz page
        // =====================================================
        [Authorize]
        [HttpGet]
        [BlockAdmin]
        public async Task<IActionResult> Pay(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only the owner of the order can pay for it
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentStatus == "Paid" || order.OrderStatus == "Cancelled")
            {
                return RedirectToAction("Details", "Orders", new { id });
            }

            var baseUrl = _config["App:BaseUrl"]
                          ?? $"{Request.Scheme}://{Request.Host}";

            var result = await _ssl.CreateSessionAsync(order, baseUrl);

            if (!result.Ok)
            {
                TempData["ErrorMessage"] =
                    "Payment gateway is not available right now. Please try again in a moment.";

                return RedirectToAction("Details", "Orders", new { id });
            }

            order.PaymentMethod = "SSLCommerz";
            order.PaymentSessionKey = result.SessionKey;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Redirect(result.GatewayUrl!);
        }

        // =====================================================
        // SUCCESS: SSLCommerz sends the customer back here.
        // It is a POST from another site, so there is no login cookie
        // and no antiforgery token. Security comes from the server-side
        // validation (val_id), not from this request.
        // =====================================================
        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Success()
        {
            var form = await Request.ReadFormAsync();

            var (paid, order) = await ProcessPaymentAsync(form["val_id"]);

            if (order == null)
            {
                return RedirectToAction("MyOrders", "Orders");
            }

            if (paid)
            {
                TempData["SuccessMessage"] = "Payment successful. Thank you!";
                TempData["ClearCart"] = "1";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "We could not verify your payment. If money was deducted, please contact us.";
            }

            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public Task<IActionResult> Fail() =>
            HandleNotPaidAsync("Failed", "Payment failed. You can try again.");

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public Task<IActionResult> Cancel() =>
            HandleNotPaidAsync("Cancelled", "Payment cancelled. You can pay again anytime.");

        // =====================================================
        // IPN: SSLCommerz tells our server directly (server to server).
        // Needs a public URL, so it will not work on localhost.
        // That is fine, Success() already does the same work.
        // =====================================================
        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ipn()
        {
            var form = await Request.ReadFormAsync();

            await ProcessPaymentAsync(form["val_id"]);

            return Ok();
        }

        // -----------------------------------------------------
        private async Task<IActionResult> HandleNotPaidAsync(string paymentStatus, string message)
        {
            var form = await Request.ReadFormAsync();

            var orderNumber = form["tran_id"].ToString().Split('_')[0];

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                return RedirectToAction("MyOrders", "Orders");
            }

            // Never overwrite an order that is already Paid
            await _db.Orders
                .Where(o => o.Id == order.Id && o.PaymentStatus != "Paid")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, paymentStatus));

            TempData["ErrorMessage"] = message;

            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }

        // -----------------------------------------------------
        // The heart of the payment: verify with SSLCommerz, then mark as Paid
        // -----------------------------------------------------
        private async Task<(bool Paid, Order? Order)> ProcessPaymentAsync(string? valId)
        {
            if (string.IsNullOrWhiteSpace(valId))
            {
                return (false, null);
            }

            // 1. Ask SSLCommerz if this val_id is a real, successful payment
            var v = await _ssl.ValidateAsync(valId);

            if (v == null || !v.IsValid)
            {
                return (false, null);
            }

            // 2. Find the order using the tran_id that SSLCommerz returned,
            //    never the value that came from the browser
            var orderNumber = v.TranId.Split('_')[0];

            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                return (false, null);
            }

            // 3. The paid amount must match the order total
            bool currencyOk =
                string.IsNullOrEmpty(v.Currency) ||
                string.Equals(v.Currency, order.Currency, StringComparison.OrdinalIgnoreCase);

            if (v.Amount != order.TotalAmount || !currencyOk)
            {
                _logger.LogWarning(
                    "Payment mismatch for {Order}: paid {Amount} {Currency}, expected {Expected} {ExpectedCurrency}",
                    orderNumber, v.Amount, v.Currency, order.TotalAmount, order.Currency);

                return (false, order);
            }

            // 4. Mark as Paid. Only the first caller (Success or IPN) gets rows == 1,
            //    so stock and email are never done twice.
            var now = DateTime.UtcNow;
            var bankTran = v.BankTranId;

            var rows = await _db.Orders
                .Where(o => o.Id == order.Id && o.PaymentStatus != "Paid")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, "Paid")
                    .SetProperty(o => o.PaymentMethod, "SSLCommerz")
                    .SetProperty(o => o.TransactionId, bankTran)
                    .SetProperty(o => o.ValidationId, valId)
                    .SetProperty(o => o.PaidAt, (DateTime?)now)
                    .SetProperty(o => o.UpdatedAt, (DateTime?)now)
                    .SetProperty(o => o.OrderStatus,
                        o => o.OrderStatus == "Pending" ? "Confirmed" : o.OrderStatus));

            if (rows == 1)
            {
                await _orders.DeductStockAsync(order);

                // Update the in-memory copy so the email says "Payment received"
                order.PaymentStatus = "Paid";
                order.PaymentMethod = "SSLCommerz";

                await _email.SendOrderConfirmationAsync(order);
            }

            return (true, order);
        }
    }
}