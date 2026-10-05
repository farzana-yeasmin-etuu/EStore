using EStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EStore.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles =
            {
                "Customer",
                "Admin"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role)
                    );
                }
            }
        }


        // Seed existing products from image folders
        public static async Task SeedProductsAsync(
            IServiceProvider serviceProvider)
        {
            var context =
                serviceProvider.GetRequiredService<ApplicationDbContext>();

            var environment =
                serviceProvider.GetRequiredService<IWebHostEnvironment>();

            // If products already exist, don't create duplicates
            if (await context.Products.AnyAsync())
            {
                return;
            }

            var categories = new Dictionary<string, (string Name, decimal Price)>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "new-arrivals", ("New Arrivals", 1200) },
                { "women", ("Women's Collection", 1500) },
                { "men", ("Men's Collection", 1200) },
                { "kurti", ("Kurti", 1400) },
                { "saree", ("Saree", 2000) },
                { "accessories", ("Accessories", 500) },
                { "skincare", ("Skincare", 800) },
                { "makeup", ("Makeup", 700) },
                { "offers", ("Offers", 1000) }
            };


            foreach (var category in categories)
            {
                var folderName = category.Key;
                var categoryName = category.Value.Name;
                var defaultPrice = category.Value.Price;

                var folderPath = Path.Combine(
                    environment.WebRootPath,
                    "images",
                    "products",
                    folderName
                );

                if (!Directory.Exists(folderPath))
                {
                    continue;
                }


                var files = Directory
                    .GetFiles(folderPath)
                    .Where(file =>
                        file.EndsWith(
                            ".jpg",
                            StringComparison.OrdinalIgnoreCase
                        )
                        ||
                        file.EndsWith(
                            ".jpeg",
                            StringComparison.OrdinalIgnoreCase
                        )
                        ||
                        file.EndsWith(
                            ".png",
                            StringComparison.OrdinalIgnoreCase
                        )
                        ||
                        file.EndsWith(
                            ".webp",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .OrderBy(file => file)
                    .ToList();


                foreach (var file in files)
                {
                    var fileName =
                        Path.GetFileName(file);

                    var productName =
                        Path.GetFileNameWithoutExtension(fileName)
                            .Replace("_", " ")
                            .Replace("-", " ");


                    var imageUrl =
                        $"/images/products/{folderName}/{Uri.EscapeDataString(fileName)}";


                    var productExists =
                        await context.Products.AnyAsync(
                            p => p.ImageUrl == imageUrl
                        );

                    if (productExists)
                    {
                        continue;
                    }


                    var product = new Product
                    {
                        Name = productName,

                        Category = categoryName,

                        ImageUrl = imageUrl,

                        Price = defaultPrice,

                        Stock = 10,

                        Description =
                            $"Beautiful {categoryName} product from Ever Spring.",

                        IsActive = true,

                        CreatedAt = DateTime.UtcNow
                    };


                    context.Products.Add(product);
                }
            }


            await context.SaveChangesAsync();
        }
    }
}