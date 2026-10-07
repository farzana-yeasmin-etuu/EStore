namespace EStore.ViewModels
{
    public class ProductDetailsViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string Category { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}