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

        public AdminController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }


        // ============================================================
        // ADMIN DASHBOARD
        // ============================================================

        public IActionResult Index()
        {
            return View();
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

        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            return View(products);
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

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Orders()
        {
            var orders =
                await _context.Orders
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

            return View(orders);
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

            order.UpdatedAt =
                DateTime.UtcNow;


            await _context.SaveChangesAsync();


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