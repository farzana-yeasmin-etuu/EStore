using System.Globalization;
using System.Text.Json;
using EStore.Models;
using Microsoft.Extensions.Options;

namespace EStore.Services
{
    // Reads the "SslCommerz" section (StoreId and StorePassword come from User Secrets)
    public class SslCommerzSettings
    {
        public string StoreId { get; set; } = "";
        public string StorePassword { get; set; } = "";
        public bool IsSandbox { get; set; } = true;

        public string BaseUrl => IsSandbox
            ? "https://sandbox.sslcommerz.com"
            : "https://securepay.sslcommerz.com";
    }

    public record SslValidation(
        bool IsValid,
        string TranId,
        decimal Amount,
        string Currency,
        string? BankTranId);

    public class SslCommerzService
    {
        private readonly HttpClient _http;
        private readonly SslCommerzSettings _s;
        private readonly ILogger<SslCommerzService> _logger;

        public SslCommerzService(
            HttpClient http,
            IOptions<SslCommerzSettings> options,
            ILogger<SslCommerzService> logger)
        {
            _http = http;
            _s = options.Value;
            _logger = logger;
        }

        // Step 1: ask SSLCommerz for a payment page link
        public async Task<(bool Ok, string? GatewayUrl, string? SessionKey, string? Error)>
            CreateSessionAsync(Order order, string baseUrl)
        {
            // Every attempt gets a unique tran_id, so "Pay Now" retry after a failure works.
            // Format: ORDERNUMBER_HHmmss  (the order number never contains "_")
            var tranId = $"{order.OrderNumber}_{DateTime.UtcNow:HHmmss}";

            var form = new Dictionary<string, string>
            {
                ["store_id"] = _s.StoreId,
                ["store_passwd"] = _s.StorePassword,
                ["total_amount"] = order.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                ["currency"] = order.Currency,
                ["tran_id"] = tranId,

                ["success_url"] = $"{baseUrl}/Payment/Success",
                ["fail_url"] = $"{baseUrl}/Payment/Fail",
                ["cancel_url"] = $"{baseUrl}/Payment/Cancel",
                ["ipn_url"] = $"{baseUrl}/Payment/Ipn",

                ["cus_name"] = order.CustomerName,
                ["cus_email"] = order.CustomerEmail,
                ["cus_add1"] = order.ShippingAddress,
                ["cus_city"] = order.City,
                ["cus_postcode"] = order.PostCode,
                ["cus_country"] = "Bangladesh",
                ["cus_phone"] = order.CustomerPhone,

                ["shipping_method"] = "Courier",
                ["ship_name"] = order.CustomerName,
                ["ship_add1"] = order.ShippingAddress,
                ["ship_city"] = order.City,
                ["ship_postcode"] = order.PostCode,
                ["ship_country"] = "Bangladesh",

                ["num_of_item"] = order.OrderItems.Sum(i => i.Quantity).ToString(),
                ["product_name"] = $"Order {order.OrderNumber}",
                ["product_category"] = "Fashion",
                ["product_profile"] = "physical-goods"
            };

            try
            {
                using var response = await _http.PostAsync(
                    $"{_s.BaseUrl}/gwprocess/v4/api.php",
                    new FormUrlEncodedContent(form));

                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.TryGetProperty("status", out var st)
                    ? st.GetString()
                    : null;

                if (status == "SUCCESS")
                {
                    var url = root.GetProperty("GatewayPageURL").GetString();

                    var key = root.TryGetProperty("sessionkey", out var sk)
                        ? sk.GetString()
                        : null;

                    return (true, url, key, null);
                }

                var reason = root.TryGetProperty("failedreason", out var fr)
                    ? fr.GetString()
                    : "Unknown error";

                _logger.LogWarning("SSLCommerz session failed: {Reason}", reason);

                return (false, null, null, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz session request failed");

                return (false, null, null, "Payment gateway unreachable");
            }
        }

        // Step 2: after the customer comes back, ask SSLCommerz "was this really paid?"
        public async Task<SslValidation?> ValidateAsync(string valId)
        {
            var url =
                $"{_s.BaseUrl}/validator/api/validationserverAPI.php" +
                $"?val_id={Uri.EscapeDataString(valId)}" +
                $"&store_id={Uri.EscapeDataString(_s.StoreId)}" +
                $"&store_passwd={Uri.EscapeDataString(_s.StorePassword)}" +
                "&format=json";

            try
            {
                var json = await _http.GetStringAsync(url);

                // Useful while testing in sandbox. Safe to delete later.
                _logger.LogInformation("SSLCommerz validation response: {Json}", json);

                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;

                string Read(string name) =>
                    r.TryGetProperty(name, out var v)
                        ? (v.GetString() ?? "")
                        : "";

                var status = Read("status");
                var valid = status == "VALID" || status == "VALIDATED";

                decimal.TryParse(
                    Read("amount"),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var amount);

                return new SslValidation(
                    valid,
                    Read("tran_id"),
                    amount,
                    Read("currency_type"),
                    Read("bank_tran_id"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz validation failed");

                return null;
            }
        }
    }
}