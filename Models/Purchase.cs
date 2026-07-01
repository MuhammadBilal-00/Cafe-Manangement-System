using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class Purchase : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        [StringLength(100)]
        public string SupplierName { get; set; } = string.Empty;

        public int? SupplierId { get; set; }

        [Required]
        public int ItemId { get; set; }

        public int? BranchId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int QuantityPurchased { get; set; }

        public DateTime DatePurchased { get; set; } = DateTime.Now;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalCost { get; set; }

        // Workflow status: Pending / Approved / Received / Cancelled
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Received";

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("ItemId")]
        public InventoryItem Item { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }
    }
}
