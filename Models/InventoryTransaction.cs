using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class InventoryTransaction
    {
        public int Id { get; set; }

        [Required]
        public int InventoryItemId { get; set; }

        [Required]
        [StringLength(20)]
        public string TransactionType { get; set; } = string.Empty; // Stock In, Stock Out, Wastage, Expiry, Order Usage

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal QuantityBefore { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal QuantityAfter { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Required]
        public int BranchId { get; set; }

        public int? OrderId { get; set; } // For Order Usage transactions

        [StringLength(100)]
        public string? PerformedBy { get; set; } // User who performed the transaction

        // Navigation Properties
        [ForeignKey("InventoryItemId")]
        public InventoryItem InventoryItem { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }
    }
}
