using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// The immutable financial record for a completed order. Every applied discount, the
    /// tax rate, and the generated PDF path are snapshotted here so the bill never changes
    /// even if the underlying promo/partnership/tax setting is later edited.
    /// </summary>
    public class Invoice : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(30)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public int BranchId { get; set; }

        // ── Money breakdown (all snapshots) ──
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        public int? PromoCodeId { get; set; }

        [StringLength(40)]
        public string? PromoCodeText { get; set; }     // snapshot of the code applied

        [Column(TypeName = "decimal(10,2)")]
        public decimal PromoDiscount { get; set; } = 0;

        public int? PartnershipId { get; set; }

        [StringLength(180)]
        public string? PartnershipText { get; set; }    // e.g. "Standard Chartered Platinum (15%)"

        [Column(TypeName = "decimal(10,2)")]
        public decimal PartnershipDiscount { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0;        // percent applied after discounts

        [Column(TypeName = "decimal(10,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }         // final payable

        // ── Payment / fulfilment ──
        [StringLength(30)]
        public string PaymentMethod { get; set; } = "Cash"; // Cash | Card | Manual | ...

        [Required]
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Paid"; // Pending | Paid | Failed | Cancelled

        [StringLength(100)]
        public string? PaymentReference { get; set; }    // webhook / terminal transaction id

        [StringLength(300)]
        public string? PdfPath { get; set; }             // relative path to the stored PDF

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }

        // Navigation
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("PromoCodeId")]
        public PromoCode? PromoCode { get; set; }

        [ForeignKey("PartnershipId")]
        public Partnership? Partnership { get; set; }

        [NotMapped]
        public decimal TotalDiscount => PromoDiscount + PartnershipDiscount;
    }
}
