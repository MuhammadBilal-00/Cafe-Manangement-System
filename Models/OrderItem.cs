using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }
        [NotMapped]
        public decimal Subtotal => Quantity * Price;

        // Navigation Properties
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;
    }

}

