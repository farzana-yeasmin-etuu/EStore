
using System.ComponentModel.DataAnnotations;

namespace EStore.Models
{
    public class AIChatRequest
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Message { get; set; } = "";
    }
}
