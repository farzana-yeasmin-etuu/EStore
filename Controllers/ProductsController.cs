using EStore.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EStore.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public ProductsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        // Category pages
        public IActionResult Category(string id)
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

            var products = GetProducts(
                selectedCategory.Key,
                selectedCategory.Value
            );

            ViewBag.CategoryName = selectedCategory.Value;

            return View(products);
        }


        // Global product search
        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return RedirectToAction("Index", "Home");
            }

            var allProducts = new List<ProductViewModel>();

            foreach (var category in GetCategories())
            {
                var products = GetProducts(category.Key, category.Value);

                allProducts.AddRange(products);
            }

            var results = allProducts
                .Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            ViewBag.SearchQuery = q;

            return View(results);
        }


        // Get products from folder
        private List<ProductViewModel> GetProducts(
            string folderName,
            string categoryName)
        {
            var products = new List<ProductViewModel>();

            var folderPath = Path.Combine(
                _environment.WebRootPath,
                "images",
                "products",
                folderName
            );

            if (!Directory.Exists(folderPath))
            {
                return products;
            }

            var files = Directory
                .GetFiles(folderPath)
                .Where(file =>
                    file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(file => file)
                .ToList();

            int id = 1;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                var productName = Path.GetFileNameWithoutExtension(fileName);

                products.Add(new ProductViewModel
                {
                    Id = id,
                    ProductId = $"{folderName}-{id}",

                    Name = FormatProductName(
                        productName,
                        categoryName
                    ),

                    Category = categoryName,

                    ImageUrl =
                        $"/images/products/{folderName}/{Uri.EscapeDataString(fileName)}",

                    Price = 0
                });

                id++;
            }

            return products;
        }


        // Category list
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


        // Convert filename into product name
        private string FormatProductName(
            string fileName,
            string categoryName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return categoryName;
            }

            return fileName
                .Replace("_", " ")
                .Replace("-", " ");
        }
    }
}