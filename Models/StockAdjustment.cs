using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 3: a deliberate stock correction (damage, count, wastage). Approving it applies each
    /// line's signed delta atomically to the branch's inventory.
    /// </summary>
    public class StockAdjustment : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BranchId { get; set; }

        /// <summary>Increase | Decrease | Recount (Recount = set to a target via signed deltas)</summary>
        [Required][StringLength(20)] public string Type { get; set; } = "Decrease";

        [Required][StringLength(200)] public string Reason { get; set; } = string.Empty;

        /// <summary>Pending | Approved | Rejected</summary>
        [Required][StringLength(20)] public string ApprovalStatus { get; set; } = "Pending";

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [ForeignKey("BranchId")] public Branch? Branch { get; set; }
        [ForeignKey("CreatedById")] public User? CreatedBy { get; set; }
        [ForeignKey("ApprovedById")] public User? ApprovedBy { get; set; }
        public ICollection<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();
    }

    public class StockAdjustmentLine : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int StockAdjustmentId { get; set; }
        [Required] public int InventoryItemId { get; set; }

        /// <summary>Signed change: +5 adds, -3 removes.</summary>
        [Column(TypeName = "decimal(10,2)")] public decimal QuantityDelta { get; set; }

        [ForeignKey("StockAdjustmentId")] public StockAdjustment? StockAdjustment { get; set; }
        [ForeignKey("InventoryItemId")] public InventoryItem? InventoryItem { get; set; }
    }
}
