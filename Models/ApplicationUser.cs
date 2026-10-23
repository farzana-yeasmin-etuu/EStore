using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EStore.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // Default delivery address (used to fill the checkout form)
        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(20)]
        public string? PostCode { get; set; }
    }
}