using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class InventoryItem : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int Quantity { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Reorder level cannot be negative.")]
        public int ReorderLevel { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum stock cannot be negative.")]
        public int MinimumStock { get; set; } = 0;

        [StringLength(100)]
        public string? StorageLocation { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public DateTime? LastRestockedDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? SellingCost { get; set; }

        public int? SupplierId { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}
