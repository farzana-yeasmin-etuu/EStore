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


        // ============================================================
        // CATEGORY → FOLDER NAME
        // ============================================================

        private string GetCategoryFolder(string category)
        {
            return category switch
            {
                "New Arrivals" => "new-arrivals",
                "Women's Collection" => "women",
                "Men's Collection" => "men",
                "Kurti" => "kurti",
                "Saree" => "saree",
                "Accessories" => "accessories",
                "Skincare" => "skincare",
                "Makeup" => "makeup",
                "Offers" => "offers",

                _ => "new-arrivals"
            };
        }
    }
}