using Cafe.Models.DTOs;
using System.ComponentModel.DataAnnotations;
namespace Cafe.Models
{
    // DTOs for complex operations
    public class OrderCreateDto
    {
        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int BranchId { get; set; }

        public string? Notes { get; set; }

        public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
    }
}