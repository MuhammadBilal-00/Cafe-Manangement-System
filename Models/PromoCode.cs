using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// A discount code a cashier can apply at checkout before payment.
    /// DiscountType is "Percentage" (DiscountValue = 0–100) or "Flat" (DiscountValue = currency amount).
    /// </summary>
    public class PromoCode : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        [StringLength(40)]
        public string Code { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string DiscountType { get; set; } = "Percentage"; // Percentage | Flat

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountValue { get; set; }

        /// <summary>Cart must reach this amount for the code to apply (0 = no minimum).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal MinimumOrderAmount { get; set; } = 0;

        /// <summary>Optional cap on the discount amount for percentage codes (null = uncapped).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime ValidFrom { get; set; } = DateTime.Now;
        public DateTime ValidUntil { get; set; } = DateTime.Now.AddMonths(1);

        /// <summary>Maximum total redemptions across all orders (null = unlimited).</summary>
        public int? UsageLimit { get; set; }

        public int TimesUsed { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        /// <summary>Branch this code applies to. Null = valid at all branches.</summary>
        public int? BranchId { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }

        /// <summary>True when active, within the date window, and under its usage limit.</summary>
        [NotMapped]
        public bool IsCurrentlyValid =>
            IsActive
            && DateTime.Now >= ValidFrom
            && DateTime.Now <= ValidUntil
            && (UsageLimit == null || TimesUsed < UsageLimit.Value);
    }
}
