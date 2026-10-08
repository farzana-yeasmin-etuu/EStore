using System.ComponentModel.DataAnnotations;

namespace EStore.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string OrderNumber { get; set; } = "";

        // Logged-in user's Identity Id
        public string? UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = "";

        [Required]
        [MaxLength(150)]
        public string CustomerEmail { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string CustomerPhone { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string ShippingAddress { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string PostCode { get; set; } = "";

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "BDT";

        public decimal TotalAmount { get; set; }

        // Pending / Paid / Failed / Cancelled
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Pending";

        // Pending / Confirmed / Processing / Shipped / Delivered / Cancelled
        [MaxLength(50)]
        public string OrderStatus { get; set; } = "Pending";

        // Payment gateway transaction id
        [MaxLength(100)]
        public string? TransactionId { get; set; }

        // Payment validation id
        [MaxLength(100)]
        public string? ValidationId { get; set; }

        // Will be useful later for payment gateway
        [MaxLength(100)]
        public string? PaymentSessionKey { get; set; }

        // Prevent stock from being deducted twice later
        public bool StockDeducted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}