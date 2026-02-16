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
        [StringLength(20)]
        public string Unit { get; set; } // kg, liters, pieces, packs, etc.

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal ReorderLevel { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

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
