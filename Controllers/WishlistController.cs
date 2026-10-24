using System.Security.Claims;
using EStore.Data;
using EStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EStore.Filters;

namespace EStore.Controllers
{
    // Every action needs a logged-in user, and only touches that user's own rows
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _db;

        public WishlistController(ApplicationDbContext db)
        {
            _db = db;
        }

        private string UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)!;


        // The navbar count and heart state load from here
        [HttpGet]
        public async Task<IActionResult> Items()
        {
            var userId = UserId;

            var items = await _db.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    id = w.ProductId,
                    name = w.Product.Name,
                    category = w.Product.Category,
                    imageUrl = w.Product.ImageUrl,
                    price = w.Product.Price
                })
                .ToListAsync();

            return Json(items);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [BlockAdmin]
        public async Task<IActionResult> Add(int productId)
        {
            var userId = UserId;

            var productExists = await _db.Products
                .AnyAsync(p => p.Id == productId && p.IsActive);

            if (!productExists)
            {
                return NotFound();
            }

            var alreadySaved = await _db.WishlistItems
                .AnyAsync(w => w.UserId == userId && w.ProductId == productId);

            if (!alreadySaved)
            {
                _db.WishlistItems.Add(new WishlistItem
                {
                    UserId = userId,
                    ProductId = productId
                });

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Two quick clicks: the unique index stopped the second one. That is fine.
                }
            }

            var count = await _db.WishlistItems.CountAsync(w => w.UserId == userId);

            return Json(new { added = !alreadySaved, count });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [BlockAdmin]
        public async Task<IActionResult> Remove(int productId)
        {
            var userId = UserId;

            await _db.WishlistItems
                .Where(w => w.UserId == userId && w.ProductId == productId)
                .ExecuteDeleteAsync();

            var count = await _db.WishlistItems.CountAsync(w => w.UserId == userId);

            return Json(new { count });
        }
    }
}