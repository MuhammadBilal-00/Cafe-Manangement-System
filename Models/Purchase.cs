using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Purchase
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SupplierName { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int QuantityPurchased { get; set; }

        public DateTime DatePurchased { get; set; } = DateTime.Now;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalCost { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        // Navigation Properties
        [ForeignKey("ItemId")]
        public InventoryItem Item { get; set; }
    }
}
