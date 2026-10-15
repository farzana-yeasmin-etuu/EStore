
using System.Text.RegularExpressions;
using EStore.Data;
using EStore.Models;
using EStore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EStore.Controllers
{
    public class AIShoppingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _gemini;
        private readonly ILogger<AIShoppingController> _logger;

        public AIShoppingController(
            ApplicationDbContext context,
            GeminiService gemini,
            ILogger<AIShoppingController> logger)
        {
            _context = context;
            _gemini = gemini;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Chat(
            [FromForm] AIChatRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    reply = "Please enter a message first.",
                    products = Array.Empty<object>()
                });
            }

            try
            {
                var message = request.Message.Trim();

                var catalog = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.Stock > 0)
                    .OrderBy(p => p.Name)
                    .Take(100)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Category,
                        p.Price,
                        p.Stock,
                        p.Description,
                        p.ImageUrl
                    })
                    .ToListAsync(cancellationToken);

                // Extract a budget such as 1500 from "under 1500 taka".
                decimal? budget = null;
                var budgetMatch = Regex.Match(
                    message,
                    @"(?:under|below|within|less than|maximum|max|budget|৳|tk|taka)\s*৳?\s*(\d+(?:\.\d+)?)",
                    RegexOptions.IgnoreCase);

                if (!budgetMatch.Success)
                {
                    // Also recognize forms like "1500 taka er moddhe".
                    budgetMatch = Regex.Match(
                        message,
                        @"(\d+(?:\.\d+)?)\s*(?:taka|tk|৳|bdt)",
                        RegexOptions.IgnoreCase);
                }

                if (budgetMatch.Success &&
                    decimal.TryParse(
                        budgetMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedBudget))
                {
                    budget = parsedBudget;
                }

                // Exclude common words and numbers from product keyword matching.
                string[] stopWords =
                {
                    "show", "me", "find", "some", "please", "want",
                    "need", "under", "below", "within", "less", "than",
                    "maximum", "max", "budget", "taka", "tk", "bdt",
                    "price", "product", "products", "for", "the", "and",
                    "with", "what", "which", "available", "is", "are"
                };

                var keywords = Regex.Matches(
                        message.ToLowerInvariant(),
                        @"[\p{L}\p{N}]+")
                    .Select(m => m.Value)
                    .Where(w => w.Length >= 2 &&
                                !stopWords.Contains(w) &&
                                !decimal.TryParse(w, out _))
                    .Distinct()
                    .Take(8)
                    .ToArray();

                var matchedProducts = catalog
                    .Where(p => budget == null || p.Price <= budget.Value)
                    .Where(p =>
                        keywords.Length == 0 ||
                        keywords.Any(w =>
                            p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                            p.Category.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                            (p.Description?.Contains(
                                w, StringComparison.OrdinalIgnoreCase) ?? false)))
                    .OrderBy(p => p.Price)
                    .Take(4)
                    .ToList();

                string catalogueForAI = string.Join(
                    "\n",
                    catalog.Take(60).Select(p =>
                        $"ID: {p.Id}; Name: {p.Name}; " +
                        $"Category: {p.Category}; Price: BDT {p.Price:0.00}; " +
                        $"Stock: {p.Stock}; Description: {p.Description ?? "Not provided"}"));

                string prompt = $"""
                You are the friendly shopping assistant for Ever Spring,
                an online shop in Bangladesh.

                Reply in the same language as the customer. Bangla or Banglish
                is acceptable when the customer uses it. Keep replies concise.

                Only recommend products that exist in the catalogue below.
                Never invent names, prices, discounts, or stock quantities.
                Prices are in Bangladeshi taka (BDT).
                If the catalogue does not contain a suitable product, say so.
                Never claim to have placed an order or received payment.

                PRODUCT CATALOGUE FROM THE DATABASE:
                {catalogueForAI}

                CUSTOMER MESSAGE:
                {message}
                """;

                string reply;

                try
                {
                    reply = await _gemini.GenerateReplyAsync(
                        prompt, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Gemini may be temporarily unavailable.
                    // Continue with real database products instead.
                    _logger.LogWarning(
                        ex,
                        "Gemini unavailable. Using database fallback for the shopping assistant.");

                    if (catalog.Count == 0)
                    {
                        reply =
                            "I'm unable to connect to the AI service right now, " +
                            "and there are no in-stock products to recommend. Please try again later.";
                    }
                    else if (matchedProducts.Count > 0)
                    {
                        reply = budget.HasValue
                            ? $"I'm having trouble connecting to the AI service, but I found {matchedProducts.Count} product(s) in stock matching your request and budget of BDT {budget.Value:0.##}. Here they are:"
                            : $"I'm having trouble connecting to the AI service, but I found {matchedProducts.Count} product(s) that may match your request. Here they are:";
                    }
                    else if (budget.HasValue)
                    {
                        reply =
                            $"The AI service is temporarily unavailable. " +
                            $"I couldn't find an in-stock product matching those keywords under BDT {budget.Value:0.##}. " +
                            "Try a different category or a higher budget.";
                    }
                    else
                    {
                        reply =
                            "The AI service is temporarily unavailable. " +
                            "Please try a product name or category such as kurti, saree, skincare, or makeup.";
                    }
                }

                var products = matchedProducts.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Category,
                    Price = p.Price.ToString(
                        "0.00",
                        System.Globalization.CultureInfo.InvariantCulture),
                    p.Stock,
                    p.ImageUrl
                }).ToList();

                return Json(new { reply, products });
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI shopping chat failed.");

                return StatusCode(500, new
                {
                    reply = "Sorry, I couldn't process your request. Please try again.",
                    products = Array.Empty<object>()
                });
            }
        }
    }
}
