using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 4: goods returned by a customer. Approving restocks the returned inventory and credits
    /// the customer (reduces their receivable / creates store credit).
    /// </summary>
    public class SellReturn : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BranchId { get; set; }
        public int? CustomerId { get; set; }
        public int? InvoiceId { get; set; }

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
        [ForeignKey("CustomerId")] public User? Customer { get; set; }
        public ICollection<SellReturnLine> Lines { get; set; } = new List<SellReturnLine>();
    }

    public class SellReturnLine : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int SellReturnId { get; set; }
        /// <summary>Inventory item restocked when approved.</summary>
        [Required] public int InventoryItemId { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal Quantity { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal UnitValue { get; set; }

        [ForeignKey("SellReturnId")] public SellReturn? SellReturn { get; set; }
        [ForeignKey("InventoryItemId")] public InventoryItem? InventoryItem { get; set; }
    }
}
