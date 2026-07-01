using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 3: manufacturing — consumes input inventory to produce an output item, rolling the
    /// input cost up into a manufactured unit cost.
    /// </summary>
    public class ProductionOrder : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BranchId { get; set; }

        [Required] public int OutputInventoryItemId { get; set; }
        [Column(TypeName = "decimal(10,2)")][Range(0.01, double.MaxValue)] public decimal OutputQuantity { get; set; }

        /// <summary>Draft | Completed | Cancelled</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Draft";

        /// <summary>Rolled-up totals set on completion.</summary>
        [Column(TypeName = "decimal(12,2)")] public decimal TotalInputCost { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal UnitCost { get; set; }

        [StringLength(400)] public string? Notes { get; set; }
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        [ForeignKey("BranchId")] public Branch? Branch { get; set; }
        [ForeignKey("OutputInventoryItemId")] public InventoryItem? OutputItem { get; set; }
        [ForeignKey("CreatedById")] public User? CreatedBy { get; set; }
        public ICollection<ProductionInput> Inputs { get; set; } = new List<ProductionInput>();
    }

    public class ProductionInput : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int ProductionOrderId { get; set; }
        [Required] public int InventoryItemId { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal Quantity { get; set; }

        [ForeignKey("ProductionOrderId")] public ProductionOrder? ProductionOrder { get; set; }
        [ForeignKey("InventoryItemId")] public InventoryItem? InventoryItem { get; set; }
    }
}
