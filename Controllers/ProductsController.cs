
using EStore.Data;
using EStore.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
 
namespace EStore.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }


        // =========================================================
        // Check if the image file still exists in wwwroot
        // =========================================================
        private bool ImageExists(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return false;
            }

            var relativePath = Uri
                .UnescapeDataString(imageUrl)
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath
            );

            return System.IO.File.Exists(fullPath);
        }


        // =========================================================
        // CATEGORY PAGE
        // =========================================================
        public async Task<IActionResult> Category(string id)
        {
            var categories = GetCategories();

            var selectedCategory = categories.FirstOrDefault(
                x => x.Key.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (selectedCategory.Key == null)
            {
                return NotFound();
            }


            var products = await _context.Products
                .Where(p =>
                    p.Category == selectedCategory.Value &&
                    p.IsActive
                )
                .OrderBy(p => p.Id)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    ProductId = p.Id.ToString(),

                    Name = p.Name,

                    Category = p.Category,

                    ImageUrl = p.ImageUrl,

                    Price = p.Price
                })
                .ToListAsync();

            // Hide products whose image was deleted
            products = products
                .Where(p => ImageExists(p.ImageUrl))
                .ToList();


            ViewBag.CategoryName = selectedCategory.Value;

            return View(products);
        }


        // =========================================================
        // PRODUCT SEARCH
        // =========================================================
        public async Task<IActionResult> Search(
            string q,
            string category = "")
        {
            var query = _context.Products
                .Where(p => p.IsActive)
                .AsQueryable();


            // Category-specific search
            if (!string.IsNullOrWhiteSpace(category))
            {
                var categories = GetCategories();

                var selectedCategory = categories.FirstOrDefault(
                    x => x.Key.Equals(
                        category,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

                if (selectedCategory.Key == null)
                {
                    return NotFound();
                }

                query = query.Where(
                    p => p.Category == selectedCategory.Value
                );
            }


            // Search product name/category
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(
                    p =>
                        p.Name.Contains(q) ||
                        p.Category.Contains(q)
                );
            }


            var results = await query
                .OrderBy(p => p.Id)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    ProductId = p.Id.ToString(),

                    Name = p.Name,

                    Category = p.Category,

                    ImageUrl = p.ImageUrl,

                    Price = p.Price
                })
                .ToListAsync();

            // Hide products whose image was deleted
            results = results
                .Where(p => ImageExists(p.ImageUrl))
                .ToList();


            ViewBag.SearchQuery = q;
            ViewBag.SearchCategory = category;

            return View(results);
        }


        // =========================================================
        // CATEGORY LIST
        // =========================================================
        private Dictionary<string, string> GetCategories()
        {
            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "new-arrivals", "New Arrivals" },
                { "women", "Women's Collection" },
                { "men", "Men's Collection" },
                { "kurti", "Kurti" },
                { "saree", "Saree" },
                { "accessories", "Accessories" },
                { "skincare", "Skincare" },
                { "makeup", "Makeup" },
                { "offers", "Offers" }
            };
        }
    }
}
