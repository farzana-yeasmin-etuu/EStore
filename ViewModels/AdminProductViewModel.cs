using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EStore.ViewModels
{
    public class AdminProductViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Please select a category.")]
        public string Category { get; set; } = "";

        [Required(ErrorMessage = "Price is required.")]
        [Range(0, 10000000, ErrorMessage = "Enter a valid price.")]
        public decimal Price { get; set; }

        [Range(0, 1000000, ErrorMessage = "Enter a valid stock quantity.")]
        public int Stock { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Product Image")]
        public IFormFile? ImageFile { get; set; }

        public string? ExistingImageUrl { get; set; }

        public bool IsActive { get; set; } = true;
    }
}