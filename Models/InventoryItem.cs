using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } // Dairy, Beverage, Bakery, Raw Material, etc.

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } // kg, liters, pieces, packs, etc.

        [Required]
        [Range(0, double.MaxValue)]
        public decimal CurrentQuantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal MinimumThreshold { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal CostPerUnit { get; set; }

        [StringLength(100)]
        public string? Supplier { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "In Stock"; // In Stock, Low Stock, Out of Stock

        [Required]
        public int BranchId { get; set; }

        // Navigation Properties
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; }

        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
        public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
        public ICollection<InventoryRecipeMapping> RecipeMappings { get; set; } = new List<InventoryRecipeMapping>();
    }
}
