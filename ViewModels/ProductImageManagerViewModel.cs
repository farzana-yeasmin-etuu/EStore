using System.ComponentModel.DataAnnotations;

namespace EStore.ViewModels
{
    public class ProductImageManagerViewModel
    {
        public List<ProductImageItemViewModel> Products { get; set; }
            = new();
    }


    public class ProductImageItemViewModel
    {
        public int Id { get; set; }

        public string ImageUrl { get; set; } = "";

        [Required(ErrorMessage = "Product name is required.")]
        public string ProductName { get; set; } = "";

        public string Category { get; set; } = "";

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public bool IsActive { get; set; }
    }
}