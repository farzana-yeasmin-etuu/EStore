using System.ComponentModel.DataAnnotations;

namespace EStore.ViewModels
{
    public class CheckoutRequest
    {
        [Required]
        public string CustomerName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = "";

        [Required]
        public string CustomerPhone { get; set; } = "";

        [Required]
        public string ShippingAddress { get; set; } = "";

        [Required]
        public string City { get; set; } = "";

        [Required]
        public string PostCode { get; set; } = "";

        [Required]
        public List<CartItemRequest> Items { get; set; }
            = new List<CartItemRequest>();
    }
}