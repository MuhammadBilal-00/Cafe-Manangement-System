using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 3: moves stock between two branches. Completing a transfer deducts each line from the
    /// source branch and adds it to the destination branch in a single transaction.
    /// </summary>
    public class StockTransfer : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int FromBranchId { get; set; }
        [Required] public int ToBranchId { get; set; }

        /// <summary>Draft | Completed | Cancelled</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Draft";

        [StringLength(40)] public string? Reference { get; set; }
        [StringLength(400)] public string? Notes { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        [ForeignKey("FromBranchId")] public Branch? FromBranch { get; set; }
        [ForeignKey("ToBranchId")] public Branch? ToBranch { get; set; }
        [ForeignKey("CreatedById")] public User? CreatedBy { get; set; }
        public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
    }

    public class StockTransferItem : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int StockTransferId { get; set; }
        /// <summary>The source-branch inventory item being moved.</summary>
        [Required] public int InventoryItemId { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal Quantity { get; set; }

        [ForeignKey("StockTransferId")] public StockTransfer? StockTransfer { get; set; }
        [ForeignKey("InventoryItemId")] public InventoryItem? InventoryItem { get; set; }
    }
}
