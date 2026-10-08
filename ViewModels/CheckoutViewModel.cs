using System.ComponentModel.DataAnnotations;

namespace EStore.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string CustomerName { get; set; } = "";

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string CustomerEmail { get; set; } = "";

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string CustomerPhone { get; set; } = "";

        [Required]
        [Display(Name = "Shipping Address")]
        public string ShippingAddress { get; set; } = "";

        [Required]
        public string City { get; set; } = "";

        [Required]
        [Display(Name = "Post Code")]
        public string PostCode { get; set; } = "";
    }
}