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


// ======================================================
// Build Application
// ======================================================
var app = builder.Build();

// ======================================================
// Create default roles
// ======================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await DbInitializer.SeedRolesAsync(services);
}

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
// 10. MVC Route
// ======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);


app.Run();