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
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } // kg, liters, pieces, etc.

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        // Navigation Properties
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; }

        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}
