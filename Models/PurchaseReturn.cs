using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 4: goods returned to a supplier. Approving deducts the returned inventory and reduces
    /// the amount owed to the supplier (accounts payable).
    /// </summary>
    public class PurchaseReturn : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BranchId { get; set; }
        public int? SupplierId { get; set; }

        [Required][StringLength(30)] public string ReturnNumber { get; set; } = string.Empty;
        /// <summary>Pending | Approved | Rejected</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Pending";

        [Column(TypeName = "decimal(10,2)")] public decimal TotalAmount { get; set; }
        [StringLength(300)] public string? Reason { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [ForeignKey("BranchId")] public Branch? Branch { get; set; }
        [ForeignKey("SupplierId")] public Supplier? Supplier { get; set; }
        public ICollection<PurchaseReturnLine> Lines { get; set; } = new List<PurchaseReturnLine>();
    }

    public class PurchaseReturnLine : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int PurchaseReturnId { get; set; }
        [Required] public int InventoryItemId { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal Quantity { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal UnitCost { get; set; }

        [ForeignKey("PurchaseReturnId")] public PurchaseReturn? PurchaseReturn { get; set; }
        [ForeignKey("InventoryItemId")] public InventoryItem? InventoryItem { get; set; }
    }
}
