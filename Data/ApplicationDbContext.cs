using EStore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EStore.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        public DbSet<PageVisit> PageVisits { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<WishlistItem> WishlistItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Keep Identity tables working
            base.OnModelCreating(builder);

            builder.Entity<Order>(e =>
            {
                // Indexes make search, filter and reports fast
                e.HasIndex(o => o.OrderNumber).IsUnique();
                e.HasIndex(o => o.CreatedAt);
                e.HasIndex(o => o.UserId);
                e.HasIndex(o => o.OrderStatus);

                e.Property(o => o.TotalAmount).HasPrecision(12, 2);

                // Old orders become "COD" automatically
                e.Property(o => o.PaymentMethod).HasDefaultValue("COD");
            });

            builder.Entity<OrderItem>(e =>
            {
                e.Property(x => x.UnitPrice).HasPrecision(12, 2);
                e.Property(x => x.TotalPrice).HasPrecision(12, 2);
            });

            builder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(12, 2);

            builder.Entity<WishlistItem>(e =>
            {
                // A user can save the same product only once
                e.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

                e.HasOne(w => w.User)
                    .WithMany()
                    .HasForeignKey(w => w.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(w => w.Product)
                    .WithMany()
                    .HasForeignKey(w => w.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}