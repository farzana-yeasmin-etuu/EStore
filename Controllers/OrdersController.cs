using EStore.Data;
using EStore.Models;
using EStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EStore.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: /Orders/Checkout
        // =========================================================
        [HttpGet]
        public IActionResult Checkout()
        {
            var model = new CheckoutViewModel
            {
                CustomerEmail =
                    User.FindFirstValue(ClaimTypes.Email) ?? ""
            };

            return View(model);
        }


        // =========================================================
        // POST: /Orders/CreateOrder
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CheckoutRequest request)
        {
            // -----------------------------------------------------
            // 1. Validate request
            // -----------------------------------------------------
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please fill in all required information."
                });
            }


            // -----------------------------------------------------
            // 2. Check cart
            // -----------------------------------------------------
            if (request.Items == null ||
                request.Items.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Your shopping bag is empty."
                });
            }


            // -----------------------------------------------------
            // 3. Merge duplicate product IDs
            // -----------------------------------------------------
            var requestedItems = request.Items
                .GroupBy(x => x.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .ToList();


            // -----------------------------------------------------
            // 4. Get products from DATABASE
            // -----------------------------------------------------
            var productIds =
                requestedItems
                    .Select(x => x.ProductId)
                    .ToList();

            var products =
                await _context.Products
                    .Where(p =>
                        productIds.Contains(p.Id) &&
                        p.IsActive)
                    .ToListAsync();


            // -----------------------------------------------------
            // 5. Make sure every product exists
            // -----------------------------------------------------
            if (products.Count != productIds.Count)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "One or more products are no longer available."
                });
            }


            // -----------------------------------------------------
            // 6. Calculate total from DATABASE price
            // -----------------------------------------------------
            decimal totalAmount = 0;

            var orderItems =
                new List<OrderItem>();


            foreach (var requestedItem in requestedItems)
            {
                var product =
                    products.First(
                        p => p.Id == requestedItem.ProductId);


                // -------------------------------------------------
                // Stock check
                // -------------------------------------------------
                if (requestedItem.Quantity > product.Stock)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            $"{product.Name} has only " +
                            $"{product.Stock} item(s) available."
                    });
                }


                // -------------------------------------------------
                // Calculate item total
                // -------------------------------------------------
                decimal itemTotal =
                    product.Price *
                    requestedItem.Quantity;

                totalAmount += itemTotal;


                // -------------------------------------------------
                // Create OrderItem
                // -------------------------------------------------
                orderItems.Add(
                    new OrderItem
                    {
                        ProductId = product.Id,

                        ProductName =
                            product.Name,

                        ImageUrl =
                            product.ImageUrl,

                        UnitPrice =
                            product.Price,

                        Quantity =
                            requestedItem.Quantity,

                        TotalPrice =
                            itemTotal
                    });
            }


            // -----------------------------------------------------
            // 7. Minimum/maximum order validation
            // -----------------------------------------------------
            if (totalAmount <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid order amount."
                });
            }


            // -----------------------------------------------------
            // 8. Generate Order Number
            // -----------------------------------------------------
            string orderNumber =
                $"ES-{DateTime.UtcNow:yyyyMMddHHmmss}-" +
                $"{Guid.NewGuid().ToString("N")[..6].ToUpper()}";


            // -----------------------------------------------------
            // 9. Create Order
            // -----------------------------------------------------
            var order =
                new Order
                {
                    OrderNumber =
                        orderNumber,

                    UserId =
                        User.FindFirstValue(
                            ClaimTypes.NameIdentifier),

                    CustomerName =
                        request.CustomerName.Trim(),

                    CustomerEmail =
                        request.CustomerEmail.Trim(),

                    CustomerPhone =
                        request.CustomerPhone.Trim(),

                    ShippingAddress =
                        request.ShippingAddress.Trim(),

                    City =
                        request.City.Trim(),

                    PostCode =
                        request.PostCode.Trim(),

                    Currency =
                        "BDT",

                    TotalAmount =
                        totalAmount,

                    PaymentStatus =
                        "Pending",

                    OrderStatus =
                        "Pending",

                    CreatedAt =
                        DateTime.UtcNow
                };


            // -----------------------------------------------------
            // 10. Add OrderItems
            // -----------------------------------------------------
            foreach (var item in orderItems)
            {
                order.OrderItems.Add(item);
            }


            // -----------------------------------------------------
            // 11. Save to PostgreSQL
            // -----------------------------------------------------
            _context.Orders.Add(order);

            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // 12. Return result
            // -----------------------------------------------------
            return Ok(new
            {
                success = true,

                orderId =
                    order.Id,

                orderNumber =
                    order.OrderNumber,

                totalAmount =
                    order.TotalAmount,

                message =
                    "Order created successfully."
            });
        }


        // =========================================================
        //  MY Orders
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var orders =
                await _context.Orders
                    .Include(o => o.OrderItems)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

            return View(orders);
        }
        // =========================================================
        // GET: /Orders/Details/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            var order =
                await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id &&
                        o.UserId == userId);


            if (order == null)
            {
                return NotFound();
            }


            return View(order);
        }
    }
}