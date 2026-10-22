using EStore.Data;
using EStore.Models;
using EStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EStore.Controllers
{
    [Authorize(Roles = "Admin")]

    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly EStore.Services.IEmailService _email;

        public AdminController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            EStore.Services.IEmailService email)
        {
            _context = context;
            _environment = environment;
            _email = email;
        }

        // ============================================================
        // ADMIN DASHBOARD
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            const int lowStockLimit = 5;

            // "Today" and "this month" must follow Bangladesh time, not UTC
            var nowBd = ToBd(DateTime.UtcNow);
            var todayStart = BdToUtc(nowBd);
            var monthStart = BdToUtc(new DateTime(nowBd.Year, nowBd.Month, 1));
            var lastMonthStart = BdToUtc(new DateTime(nowBd.Year, nowBd.Month, 1).AddMonths(-1));
            var chartFrom = BdToUtc(nowBd.AddDays(-29));

            var orders = _context.Orders.AsNoTracking();
            var validOrders = orders.Where(o => o.OrderStatus != "Cancelled");

            // ---------- Sales (all non-cancelled orders) ----------
            var todaySales = await validOrders
                .Where(o => o.CreatedAt >= todayStart)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var todayOrders = await validOrders
                .CountAsync(o => o.CreatedAt >= todayStart);

            var monthSales = await validOrders
                .Where(o => o.CreatedAt >= monthStart)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var monthOrders = await validOrders
                .CountAsync(o => o.CreatedAt >= monthStart);

            var lastMonthSales = await validOrders
                .Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < monthStart)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            decimal? growth = null;
            if (lastMonthSales > 0)
            {
                growth = Math.Round((monthSales - lastMonthSales) / lastMonthSales * 100, 1);
            }

            // ---------- Revenue (really paid) ----------
            var revenue = await validOrders
                .Where(o => o.PaymentStatus == "Paid")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var awaiting = await validOrders
                .Where(o => o.PaymentStatus != "Paid")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var validCount = await validOrders.CountAsync();
            var validTotal = revenue + awaiting;

            // ---------- Customers (admins are not customers) ----------
            var adminRoleId = await _context.Roles
                .Where(r => r.Name == "Admin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var adminCount = adminRoleId == null
                ? 0
                : await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId);

            var customers = await _context.Users.CountAsync() - adminCount;

            // ---------- Orders by status ----------
            var statusCounts = await orders
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            // ---------- 30-day chart ----------
            // Only the last 30 days are loaded, then grouped by Bangladesh date in memory
            var last30 = await validOrders
                .Where(o => o.CreatedAt >= chartFrom)
                .Select(o => new { o.CreatedAt, o.TotalAmount })
                .ToListAsync();

            var byDay = last30
                .GroupBy(o => ToBd(o.CreatedAt).Date)
                .ToDictionary(
                    g => g.Key,
                    g => new { Total = g.Sum(x => x.TotalAmount), Count = g.Count() });

            var vm = new AdminDashboardViewModel
            {
                TodayLabel = nowBd.ToString("dddd, dd MMMM yyyy"),

                TodaySales = todaySales,
                TodayOrders = todayOrders,
                MonthSales = monthSales,
                MonthOrders = monthOrders,
                MonthGrowthPercent = growth,

                RevenueCollected = revenue,
                AwaitingPayment = awaiting,
                AvgOrderValue = validCount == 0 ? 0 : validTotal / validCount,

                PendingOrders = statusCounts.GetValueOrDefault("Pending"),
                TotalOrders = statusCounts.Values.Sum(),
                TotalCustomers = customers,

                ActiveProducts = await _context.Products.CountAsync(p => p.IsActive),
                LowStockCount = await _context.Products
                    .CountAsync(p => p.IsActive && p.Stock > 0 && p.Stock <= lowStockLimit),
                OutOfStockCount = await _context.Products
                    .CountAsync(p => p.IsActive && p.Stock == 0),

                StatusCounts = statusCounts
            };

            for (int i = 29; i >= 0; i--)
            {
                var day = nowBd.Date.AddDays(-i);

                vm.ChartLabels.Add(day.ToString("dd MMM"));

                if (byDay.TryGetValue(day, out var row))
                {
                    vm.ChartSales.Add(row.Total);
                    vm.ChartOrders.Add(row.Count);
                }
                else
                {
                    vm.ChartSales.Add(0);
                    vm.ChartOrders.Add(0);
                }
            }

            // ---------- Top selling products (last 30 days) ----------
            var top = await _context.OrderItems
                .AsNoTracking()
                .Where(i => i.Order.CreatedAt >= chartFrom
                         && i.Order.OrderStatus != "Cancelled")
                .GroupBy(i => i.ProductName)
                .Select(g => new
                {
                    Name = g.Key,
                    Qty = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.TotalPrice)
                })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToListAsync();

            vm.TopProducts = top
                .Select(x => new TopProductRow
                {
                    Name = x.Name,
                    Qty = x.Qty,
                    Revenue = x.Revenue
                })
                .ToList();

            // ---------- Recent orders ----------
            var recent = await orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.CustomerName,
                    o.TotalAmount,
                    o.PaymentStatus,
                    o.OrderStatus,
                    o.CreatedAt
                })
                .ToListAsync();

            vm.RecentOrders = recent
                .Select(o => new RecentOrderRow
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    TotalAmount = o.TotalAmount,
                    PaymentStatus = o.PaymentStatus,
                    OrderStatus = o.OrderStatus,
                    Date = ToBd(o.CreatedAt).ToString("dd MMM, hh:mm tt")
                })
                .ToList();

            // ---------- Low stock ----------
            vm.LowStockProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && p.Stock <= lowStockLimit)
                .OrderBy(p => p.Stock)
                .ThenBy(p => p.Name)
                .Take(6)
                .Select(p => new LowStockRow
                {
                    Id = p.Id,
                    Name = p.Name,
                    Stock = p.Stock,
                    ImageUrl = p.ImageUrl
                })
                .ToListAsync();

            return View(vm);
        }


        // TEMPORARY: delete this after email test works
        [HttpGet]
        public async Task<IActionResult> TestEmail(string to)
        {
            if (string.IsNullOrWhiteSpace(to))
                return Content("Use: /Admin/TestEmail?to=youremail@gmail.com");

            var ok = await _email.SendAsync(
                to,
                "Test email from Ever Spring",
                "<h3>It works!</h3><p>Your email setup is correct.</p>");

            return Content(ok
                ? "Email sent. Check your inbox (and spam folder)."
                : "Email FAILED. Look at the terminal for the error message.");
        }


        // ============================================================
        // Page visitor
        // ============================================================

        public async Task<IActionResult> PageVisits()
        {
            var visits = _context.PageVisits.AsNoTracking();

            var totalVisits = await visits.CountAsync();

            var uniqueVisitors = await visits
                .Where(v => v.VisitorId != null && v.VisitorId != "")
                .Select(v => v.VisitorId)
                .Distinct()
                .CountAsync();

            var loggedInVisits = await visits
                .CountAsync(v => v.UserId != null && v.UserId != "");

            var guestVisits = await visits
                .CountAsync(v => v.UserId == null || v.UserId == "");

            var popularPages = await visits
                .GroupBy(v => v.PageName)
                .Select(g => new PopularPageViewModel
                {
                    PageName = g.Key,
                    Visits = g.Count()
                })
                .OrderByDescending(p => p.Visits)
                .Take(10)
                .ToListAsync();

            var recentVisits = await visits
                .OrderByDescending(v => v.VisitedAt)
                .Take(50)
                .Select(v => new RecentPageVisitViewModel
                {
                    PageName = v.PageName,
                    Url = v.Url,
                    UserId = v.UserId,
                    VisitorId = v.VisitorId,
                    VisitedAt = v.VisitedAt,
                    IsLoggedIn = v.UserId != null && v.UserId != ""
                })
                .ToListAsync();

            var model = new PageVisitDashboardViewModel
            {
                TotalVisits = totalVisits,
                UniqueVisitors = uniqueVisitors,
                LoggedInVisits = loggedInVisits,
                GuestVisits = guestVisits,
                PopularPages = popularPages,
                RecentVisits = recentVisits
            };

            return View(model);
        }


        // ============================================================
        // PRODUCT LIST
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            // Only the category names (for the filter dropdown), not all products
            ViewBag.Categories = await _context.Products
                .AsNoTracking()
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View();
        }


        // ============================================================
        // PRODUCTS DATA FOR DATATABLES (server-side)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductsData(
            [FromForm] DataTablesRequest dt,
            string? category,
            string? status,
            string? stock)
        {
            const int lowStockLimit = 5;

            var total = await _context.Products.CountAsync();

            var query = _context.Products.AsNoTracking().AsQueryable();

            // ---- Filters ----
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            if (status == "Active")
            {
                query = query.Where(p => p.IsActive);
            }
            else if (status == "Inactive")
            {
                query = query.Where(p => !p.IsActive);
            }

            if (stock == "Out")
            {
                query = query.Where(p => p.Stock == 0);
            }
            else if (stock == "Low")
            {
                query = query.Where(p => p.Stock > 0 && p.Stock <= lowStockLimit);
            }

            // ---- Search (name, category, or product ID) ----
            var search = dt.Search.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                var isId = int.TryParse(search, out var idValue);

                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, pattern) ||
                    EF.Functions.ILike(p.Category, pattern) ||
                    (isId && p.Id == idValue));
            }

            var filtered = await query.CountAsync();

            // ---- Sorting (only whitelisted columns) ----
            var order = dt.Order.FirstOrDefault();
            var asc = order?.Dir == "asc";

            IOrderedQueryable<Product> sorted = order == null
                ? query.OrderByDescending(p => p.Id)   // default: newest first
                : order.Column switch
                {
                    1 => asc ? query.OrderBy(p => p.Name)
                             : query.OrderByDescending(p => p.Name),

                    2 => asc ? query.OrderBy(p => p.Category)
                             : query.OrderByDescending(p => p.Category),

                    3 => asc ? query.OrderBy(p => p.Price)
                             : query.OrderByDescending(p => p.Price),

                    4 => asc ? query.OrderBy(p => p.Stock)
                             : query.OrderByDescending(p => p.Stock),

                    5 => asc ? query.OrderBy(p => p.IsActive)
                             : query.OrderByDescending(p => p.IsActive),

                    _ => query.OrderByDescending(p => p.Id)
                };

            var pageSize = Math.Clamp(dt.Length, 1, 100);

            var data = await sorted
                .ThenByDescending(p => p.Id)
                .Skip(dt.Start)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.Id,
                    imageUrl = p.ImageUrl,
                    name = p.Name,
                    category = p.Category,
                    price = p.Price,
                    stock = p.Stock,
                    isActive = p.IsActive
                })
                .ToListAsync();

            return Json(new
            {
                draw = dt.Draw,
                recordsTotal = total,
                recordsFiltered = filtered,
                data
            });
        }


        // ============================================================
        // ADD PRODUCT - GET
        // ============================================================

        [HttpGet]
        public IActionResult CreateProduct()
        {
            var model = new AdminProductViewModel
            {
                IsActive = true
            };

            return View(model);
        }


        // ============================================================
        // ADD PRODUCT - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(
            AdminProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.ImageFile == null ||
                model.ImageFile.Length == 0)
            {
                ModelState.AddModelError(
                    "ImageFile",
                    "Please select a product image."
                );

                return View(model);
            }

            var imageUrl = await SaveProductImage(
                model.ImageFile,
                model.Category
            );

            var product = new Product
            {
                Name = model.Name.Trim(),
                Category = model.Category,
                Price = model.Price,
                Stock = model.Stock,
                Description = model.Description,
                ImageUrl = imageUrl,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Product added successfully.";

            return RedirectToAction(nameof(Products));
        }


        // ============================================================
        // EDIT PRODUCT - GET
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            var model = new AdminProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                Price = product.Price,
                Stock = product.Stock,
                Description = product.Description,
                ExistingImageUrl = product.ImageUrl,
                IsActive = product.IsActive
            };

            return View(model);
        }


        // ============================================================
        // EDIT PRODUCT - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(
            AdminProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == model.Id);

            if (product == null)
            {
                return NotFound();
            }

            // Update normal product information
            product.Name = model.Name.Trim();
            product.Category = model.Category;
            product.Price = model.Price;
            product.Stock = model.Stock;
            product.Description = model.Description;
            product.IsActive = model.IsActive;

            // If a new image was uploaded
            if (model.ImageFile != null &&
                model.ImageFile.Length > 0)
            {
                var oldImageUrl = product.ImageUrl;

                var newImageUrl = await SaveProductImage(
                    model.ImageFile,
                    model.Category
                );

                product.ImageUrl = newImageUrl;

                // Delete old image
                DeleteProductImage(oldImageUrl);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Product updated successfully.";

            return RedirectToAction(nameof(Products));
        }


        // ============================================================
        // DEACTIVATE PRODUCT
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            /*
             * We are not permanently deleting the product.
             *
             * Instead, IsActive = false.
             *
             * This is safer for a real e-commerce system because
             * future orders may refer to this product.
             */

            product.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Product deactivated successfully.";

            return RedirectToAction(nameof(Products));
        }


        // ============================================================
        // ACTIVATE PRODUCT
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateProduct(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            product.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Product activated successfully.";

            return RedirectToAction(nameof(Products));
        }


        // ============================================================
        // IMAGE MANAGER - GET
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ImageManager()
        {
            var products = await _context.Products
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Id)
                .Select(p => new ProductImageItemViewModel
                {
                    Id = p.Id,
                    ImageUrl = p.ImageUrl,
                    ProductName = p.Name,
                    Category = p.Category,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            var model = new ProductImageManagerViewModel
            {
                Products = products
            };

            return View(model);
        }

        // ============================================================
        // IMAGE MANAGER - SAVE NAMES
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImageManager(
            ProductImageManagerViewModel model)
        {
            try
            {
                if (model.Products == null ||
                    model.Products.Count == 0)
                {
                    TempData["Error"] = "No products were received.";
                    return RedirectToAction(nameof(ImageManager));
                }

                foreach (var item in model.Products)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.Id);

                    if (product == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.ProductName))
                    {
                        continue;
                    }

                    product.Name = item.ProductName.Trim();
                }

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Product names saved successfully.";

                return RedirectToAction(nameof(ImageManager));
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Something went wrong while saving product names.";

                Console.WriteLine(ex);

                return RedirectToAction(nameof(ImageManager));
            }
        }


        // ============================================================
        // RENAME ALL PRODUCT IMAGES
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameProductImages()
        {
            try
            {
                var products = await _context.Products
                    .OrderBy(p => p.Id)
                    .ToListAsync();

                int renamedCount = 0;
                int skippedCount = 0;

                foreach (var product in products)
                {
                    if (string.IsNullOrWhiteSpace(product.ImageUrl))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!product.ImageUrl.StartsWith(
                        "/images/products/",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    // --------------------------------------------
                    // Convert URL encoded filename back to actual
                    // filename
                    // --------------------------------------------

                    var decodedUrl = Uri.UnescapeDataString(
                        product.ImageUrl
                    );

                    // Example:
                    // /images/products/new-arrivals/new (1).jpg

                    var relativePath = decodedUrl
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        );

                    var currentFilePath = Path.Combine(
                        _environment.WebRootPath,
                        relativePath
                    );

                    // --------------------------------------------
                    // Check physical file
                    // --------------------------------------------

                    if (!System.IO.File.Exists(currentFilePath))
                    {
                        skippedCount++;
                        continue;
                    }

                    // --------------------------------------------
                    // Get extension
                    // --------------------------------------------

                    var extension = Path.GetExtension(
                        currentFilePath
                    ).ToLowerInvariant();

                    // --------------------------------------------
                    // Create filename from Product Name
                    // --------------------------------------------

                    var baseFileName = CreateSafeFileName(
                        product.Name
                    );

                    if (string.IsNullOrWhiteSpace(baseFileName))
                    {
                        baseFileName =
                            $"product-{product.Id}";
                    }

                    // --------------------------------------------
                    // Get category folder
                    // --------------------------------------------

                    var folderName = GetCategoryFolder(
                        product.Category
                    );

                    var targetFolder = Path.Combine(
                        _environment.WebRootPath,
                        "images",
                        "products",
                        folderName
                    );

                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    // --------------------------------------------
                    // Create new filename
                    // --------------------------------------------

                    var newFileName =
                        baseFileName + extension;

                    var newFilePath = Path.Combine(
                        targetFolder,
                        newFileName
                    );

                    // --------------------------------------------
                    // Prevent duplicate filenames
                    // --------------------------------------------

                    int counter = 2;

                    while (
                        System.IO.File.Exists(newFilePath) &&
                        !string.Equals(
                            currentFilePath,
                            newFilePath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        newFileName =
                            $"{baseFileName}-{counter}{extension}";

                        newFilePath = Path.Combine(
                            targetFolder,
                            newFileName
                        );

                        counter++;
                    }

                    // --------------------------------------------
                    // Already correctly named
                    // --------------------------------------------

                    if (string.Equals(
                        currentFilePath,
                        newFilePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    // --------------------------------------------
                    // Rename physical file
                    // --------------------------------------------

                    System.IO.File.Move(
                        currentFilePath,
                        newFilePath
                    );

                    // --------------------------------------------
                    // Update database ImageUrl
                    // --------------------------------------------

                    product.ImageUrl =
                        "/images/products/" +
                        folderName +
                        "/" +
                        Uri.EscapeDataString(newFileName);

                    renamedCount++;
                }

                // --------------------------------------------
                // Save database changes
                // --------------------------------------------

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"{renamedCount} product image(s) renamed successfully. " +
                    $"{skippedCount} product(s) skipped.";

                return RedirectToAction(
                    "Products",
                    "Admin"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                TempData["Error"] =
                    "Something went wrong while renaming product images.";

                return RedirectToAction(
                    "Products",
                    "Admin"
                );
            }
        }

        // ============================================================
        // CREATE SAFE IMAGE FILE NAME
        // ============================================================

        private string CreateSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }


            // Convert to lowercase
            var fileName = name
                .Trim()
                .ToLowerInvariant();


            // Replace spaces with -
            fileName = fileName.Replace(
                " ",
                "-"
            );


            // Keep only letters, numbers and -
            var characters = fileName
                .Where(c =>
                    char.IsLetterOrDigit(c) ||
                    c == '-'
                )
                .ToArray();


            fileName = new string(
                characters
            );


            // Remove multiple consecutive -
            while (fileName.Contains("--"))
            {
                fileName = fileName.Replace(
                    "--",
                    "-"
                );
            }


            // Remove - from beginning/end
            return fileName.Trim('-');
        }

        // ============================================================
        // SAVE PRODUCT IMAGE
        // ============================================================

        private async Task<string> SaveProductImage(
            IFormFile imageFile,
            string category)
        {
            var folderName = GetCategoryFolder(category);

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "images",
                "products",
                folderName
            );

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var extension =
                Path.GetExtension(imageFile.FileName)
                    .ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "Only JPG, JPEG, PNG and WEBP images are allowed."
                );
            }

            // Generate unique filename
            var uniqueFileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath = Path.Combine(
                uploadFolder,
                uniqueFileName
            );

            using (var stream = new FileStream(
                filePath,
                FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return
                $"/images/products/{folderName}/{uniqueFileName}";
        }


        // ============================================================
        // DELETE IMAGE FILE
        // ============================================================

        private void DeleteProductImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            // Only delete files inside our product image folder
            if (!imageUrl.StartsWith(
                "/images/products/",
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativePath =
                imageUrl.TrimStart('/')
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    );

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath
            );

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }


        // =========================================================
        // ADMIN ORDERS
        // =========================================================


        // =========================================================
        //  Time zone helpers (Bangladesh is UTC+6)
        // =========================================================

        private static readonly TimeZoneInfo BdTz = ResolveBdTimeZone();

        private static TimeZoneInfo ResolveBdTimeZone()
        {
            foreach (var id in new[] { "Asia/Dhaka", "Bangladesh Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch
                {
                    // try next id
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone(
                "BST", TimeSpan.FromHours(6), "Bangladesh", "Bangladesh");
        }

        // Date picked in the filter (Bangladesh time) -> UTC for the database
        private static DateTime BdToUtc(DateTime date) =>
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
                BdTz);

        // UTC from the database -> Bangladesh time for display
        private static DateTime ToBd(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                BdTz);


        // =========================================================
        //  Shared order filter (the report page will reuse this later)
        // =========================================================

        private IQueryable<Order> FilterOrders(
            string? status,
            string? paymentStatus,
            DateTime? from,
            DateTime? to,
            string? search)
        {
            var q = _context.Orders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                q = q.Where(o => o.OrderStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                q = q.Where(o => o.PaymentStatus == paymentStatus);
            }

            if (from.HasValue)
            {
                var fromUtc = BdToUtc(from.Value);
                q = q.Where(o => o.CreatedAt >= fromUtc);
            }

            if (to.HasValue)
            {
                // Include the whole "to" day
                var toUtc = BdToUtc(to.Value.AddDays(1));
                q = q.Where(o => o.CreatedAt < toUtc);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";

                q = q.Where(o =>
                    EF.Functions.ILike(o.OrderNumber, pattern) ||
                    EF.Functions.ILike(o.CustomerName, pattern) ||
                    EF.Functions.ILike(o.CustomerEmail, pattern) ||
                    EF.Functions.ILike(o.CustomerPhone, pattern));
            }

            return q;
        }


        // =========================================================
        //  Orders page (the table loads its own data)
        // =========================================================

        [HttpGet]
        public IActionResult Orders()
        {
            return View();
        }


        // =========================================================
        //  Orders data for DataTables (server-side)
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrdersData(
            [FromForm] DataTablesRequest dt,
            string? status,
            string? paymentStatus,
            DateTime? from,
            DateTime? to)
        {
            var total = await _context.Orders.CountAsync();

            var query = FilterOrders(
                status, paymentStatus, from, to, dt.Search.Value);

            var filtered = await query.CountAsync();

            // Which column and direction to sort by
            var column = dt.Order.FirstOrDefault()?.Column ?? 2;
            var asc = (dt.Order.FirstOrDefault()?.Dir ?? "desc") == "asc";

            // Only these columns are allowed. Never build ORDER BY from raw user text.
            IOrderedQueryable<Order> sorted = column switch
            {
                0 => asc ? query.OrderBy(o => o.OrderNumber)
                         : query.OrderByDescending(o => o.OrderNumber),

                1 => asc ? query.OrderBy(o => o.CustomerName)
                         : query.OrderByDescending(o => o.CustomerName),

                3 => asc ? query.OrderBy(o => o.TotalAmount)
                         : query.OrderByDescending(o => o.TotalAmount),

                4 => asc ? query.OrderBy(o => o.PaymentStatus)
                         : query.OrderByDescending(o => o.PaymentStatus),

                5 => asc ? query.OrderBy(o => o.OrderStatus)
                         : query.OrderByDescending(o => o.OrderStatus),

                _ => asc ? query.OrderBy(o => o.CreatedAt)
                         : query.OrderByDescending(o => o.CreatedAt)
            };

            var pageSize = Math.Clamp(dt.Length, 1, 100);

            var rows = await sorted
                .ThenByDescending(o => o.Id)
                .Skip(dt.Start)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.CustomerName,
                    o.CustomerPhone,
                    o.CreatedAt,
                    o.TotalAmount,
                    o.PaymentStatus,
                    o.PaymentMethod,
                    o.OrderStatus
                })
                .ToListAsync();

            var data = rows.Select(o => new
            {
                id = o.Id,
                orderNumber = o.OrderNumber,
                customer = o.CustomerName,
                phone = o.CustomerPhone,
                date = ToBd(o.CreatedAt).ToString("dd MMM yyyy, hh:mm tt"),
                total = o.TotalAmount,
                paymentStatus = o.PaymentStatus,
                paymentMethod = o.PaymentMethod,
                status = o.OrderStatus
            });

            return Json(new
            {
                draw = dt.Draw,
                recordsTotal = total,
                recordsFiltered = filtered,
                data
            });
        }



        // =========================================================
        //  OrderDetails
        // =========================================================


        public async Task<IActionResult> OrderDetails(int id)
        {
            var order =
                await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // =========================================================
        // UPDATE ORDER STATUS
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(
            int id,
            string status)
        {
            var allowedStatuses =
                new[]
                {
            "Pending",
            "Confirmed",
            "Processing",
            "Shipped",
            "Delivered",
            "Cancelled"
                };


            if (!allowedStatuses.Contains(status))
            {
                return BadRequest(
                    "Invalid order status.");
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.Id == id);


            if (order == null)
            {
                return NotFound();
            }


            order.OrderStatus = status;

            // Cash on Delivery: money is collected when the order is delivered
            if (status == "Delivered" && order.PaymentMethod == "COD")
            {
                order.PaymentStatus = "Paid";
                order.PaidAt = DateTime.UtcNow;
            }

            order.UpdatedAt =
                DateTime.UtcNow;


            await _context.SaveChangesAsync();

            // Tell the customer about the new status
            await _email.SendOrderStatusAsync(order);


            TempData["SuccessMessage"] =
                "Order status updated successfully.";


            return RedirectToAction(
                nameof(OrderDetails),
                new { id = id }
            );
        }


        // ============================================================
        // CATEGORY → FOLDER NAME
        // ============================================================

        private string GetCategoryFolder(string category)
        {
            return category switch
            {
                "New Arrivals" =>
                    "new-arrivals",

                "Women's Collection" =>
                    "women",

                "Men's Collection" =>
                    "men",

                "Kurti" =>
                    "kurti",

                "Saree" =>
                    "saree",

                "Accessories" =>
                    "accessories",

                "Skincare" =>
                    "skincare",

                "Makeup" =>
                    "makeup",

                "Offers" =>
                    "offers",

                _ =>
                    "new-arrivals"
            };
        }




    }
}