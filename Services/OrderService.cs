using EStore.Data;
using EStore.Models;
using Microsoft.EntityFrameworkCore;

namespace EStore.Services
{
    public class OrderService
    {
        private readonly ApplicationDbContext _db;

        public OrderService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Reduces product stock for every item in the order.
        // Safe to call twice: the StockDeducted flag makes sure stock is reduced only once.
        public async Task DeductStockAsync(Order order)
        {
            // Atomic "claim": only the first caller gets claimed == 1
            var claimed = await _db.Orders
                .Where(o => o.Id == order.Id && !o.StockDeducted)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.StockDeducted, true));

            if (claimed == 0)
            {
                return;
            }

            foreach (var item in order.OrderItems)
            {
                var productId = item.ProductId;
                var qty = item.Quantity;

                // Stock never goes below 0
                await _db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        p => p.Stock,
                        p => p.Stock >= qty ? p.Stock - qty : 0));
            }
        }
    }
}