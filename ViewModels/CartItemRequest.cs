using System.ComponentModel.DataAnnotations;

namespace EStore.ViewModels
{
    public class CartItemRequest
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}