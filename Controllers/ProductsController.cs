
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
        public IActionResult Search(string q, string category = "")
        {
            var products = _context.Products
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                var categoryMap = GetCategories();

                if (categoryMap.ContainsKey(category))
                {
                    var categoryName = categoryMap[category];

                    products = products.Where(p =>
                        p.Category == categoryName);
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                products = products.Where(p =>
                    p.Name.Contains(q) ||
                    p.Category.Contains(q));
            }

            var result = products
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    ImageUrl = p.ImageUrl,
                    Price = p.Price
                })
                .ToList();

            return View(result);
        }

        // =========================================================
        // Details Page
        // =========================================================
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Where(p =>
                    p.Id == id &&
                    p.IsActive
                )
                .Select(p => new ProductDetailsViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    ImageUrl = p.ImageUrl,
                    Price = p.Price,
                    Stock = p.Stock,
                    Description = p.Description,
                    IsActive = p.IsActive
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // =========================================================
        // Wishlist page action
        // =========================================================

        public IActionResult Wishlist()
        {
            return View();
        }

        // =========================================================
        // SHOPPING CART
        // =========================================================

        [HttpGet]
        public IActionResult Cart()
        {
            return View();
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
