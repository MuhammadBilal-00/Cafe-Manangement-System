using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.DTOs
{
    public class OrderItemDto
    {
 
            [Required]
            public int MenuItemId { get; set; }

            [Required]
            [Range(1, int.MaxValue)]
            public int Quantity { get; set; }
        }
}
