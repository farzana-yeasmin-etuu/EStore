using EStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EStore.Data
{
    public static class DbInitializer
    {
        // ============================================================
        // SEED ROLES
        // ============================================================
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


        // ============================================================
        // SEED ADMIN ACCOUNT
        // ============================================================
        public static async Task SeedAdminAsync(
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            var userManager =
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var adminEmail =
                configuration["AdminSettings:Email"];

            var adminPassword =
                configuration["AdminSettings:Password"];

            // Safety check
            if (string.IsNullOrWhiteSpace(adminEmail) ||
                string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new Exception(
                    "Admin email or password is missing in appsettings.json."
                );
            }

            // Check whether Admin user already exists
            var adminUser =
                await userManager.FindByEmailAsync(adminEmail);

            // If Admin user doesn't exist, create one
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult =
                    await userManager.CreateAsync(
                        adminUser,
                        adminPassword
                    );

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        createResult.Errors.Select(e => e.Description)
                    );

                    throw new Exception(
                        $"Admin account creation failed: {errors}"
                    );
                }
            }

            // Make sure Admin role exists
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(
                    adminUser,
                    "Admin"
                );
            }
        }


        // ============================================================
        // SEED PRODUCTS
        // ============================================================
        public static async Task SeedProductsAsync(
            IServiceProvider serviceProvider)
        {
            var context =
                serviceProvider.GetRequiredService<ApplicationDbContext>();

            var environment =
                serviceProvider.GetRequiredService<IWebHostEnvironment>();

            var categories =
                new Dictionary<string, (string Name, decimal Price)>(
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