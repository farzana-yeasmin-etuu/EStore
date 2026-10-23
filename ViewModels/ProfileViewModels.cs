using System.ComponentModel.DataAnnotations;

namespace EStore.ViewModels
{
    // Everything the profile page shows
    public class ProfileViewModel
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Initials { get; set; } = "";

        public int TotalOrders { get; set; }
        public int ActiveOrders { get; set; }
        public int WishlistCount { get; set; }
        public decimal TotalSpent { get; set; }

        public List<ProfileOrderRow> RecentOrders { get; set; } = new();

        // The two forms on the page
        public UpdateProfileViewModel Profile { get; set; } = new();
        public ChangePasswordViewModel Password { get; set; } = new();
    }

    public class ProfileOrderRow
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = "";
        public string Date { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "";
    }

    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(20)]
        [Display(Name = "Post Code")]
        public string? PostCode { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Please enter a new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The two passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = "";
    }
}