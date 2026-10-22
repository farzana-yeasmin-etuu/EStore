using System.ComponentModel.DataAnnotations;

namespace EStore.Models
{
    // One row = one product saved by one user
    public class WishlistItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public ApplicationUser User { get; set; } = null!;

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}