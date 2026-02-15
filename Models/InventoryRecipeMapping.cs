using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class InventoryRecipeMapping
    {
        public int Id { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        public int InventoryItemId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal QuantityRequired { get; set; } // Quantity of inventory item needed per menu item

        [StringLength(20)]
        public string Unit { get; set; } = ""; // Will be populated from inventory item unit

        // Navigation Properties
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; }

        [ForeignKey("InventoryItemId")]
        public InventoryItem InventoryItem { get; set; }
    }
}
