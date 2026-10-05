using System.ComponentModel.DataAnnotations;

namespace EStore.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = "";

        [Required]
        public string ImageUrl { get; set; } = "";

        [Required]
        [Range(0, 10000000)]
        public decimal Price { get; set; }

        public int Stock { get; set; } = 0;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}