using EStore.Data;
using EStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// 1. Add MVC
// ======================================================
builder.Services.AddControllersWithViews();


// ======================================================
// 2. Connect PostgreSQL Database
// ======================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));


// ======================================================
// 3. Configure ASP.NET Core Identity
// ======================================================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpClient<EStore.Services.GeminiService>(
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(12);
    });

//email verification

builder.Services.Configure<EStore.Services.SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<EStore.Services.IEmailService, EStore.Services.EmailService>();

//order services for COD

builder.Services.AddScoped<EStore.Services.OrderService>();


builder.Services.Configure<EStore.Services.SslCommerzSettings>(
    builder.Configuration.GetSection("SslCommerz"));

builder.Services.AddHttpClient<EStore.Services.SslCommerzService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

// ======================================================
// Build Application
// ======================================================
var app = builder.Build();

// ======================================================
// Create default roles
// ======================================================


// ======================================================
// 4. Error Handling
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// ======================================================
// 5. HTTPS
// ======================================================
app.UseHttpsRedirection();


// ======================================================
// 6. Static Files
// ======================================================
app.UseStaticFiles();


// ======================================================
// 7. Routing
// ======================================================
app.UseRouting();


// ======================================================
// 8. Authentication Middleware
// ======================================================
app.UseAuthentication();


// ======================================================
// 9. Authorization Middleware
// ======================================================
app.UseAuthorization();


// ======================================================
// 9. page visitor Middleware


app.UseMiddleware<EStore.Middleware.PageVisitMiddleware>();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Create Customer and Admin roles
    await DbInitializer.SeedRolesAsync(services);

    // Create Admin account and assign Admin role
    await DbInitializer.SeedAdminAsync(
        services,
        builder.Configuration
    );

    // Seed products from product image folders
    await DbInitializer.SeedProductsAsync(services);
}


// ======================================================
// 10. MVC Route
// ======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);


app.Run();