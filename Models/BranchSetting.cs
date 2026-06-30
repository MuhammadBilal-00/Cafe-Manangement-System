using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Per-branch checkout configuration. One row per branch (created on demand).
    /// Holds the optional-hardware toggle, the tax rate applied to invoices, and an
    /// optional footer line printed on PDF bills.
    /// </summary>
    public class BranchSetting
    {
        public int Id { get; set; }

        [Required]
        public int BranchId { get; set; }

        /// <summary>
        /// When true, the order is only closed once a payment webhook pings success.
        /// When false, clicking "Pay" closes the invoice immediately (manual cashier confirmation).
        /// </summary>
        public bool HardwareTerminalEnabled { get; set; } = false;

        /// <summary>Tax percentage applied to the post-discount subtotal (0 = no tax).</summary>
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal TaxRatePercent { get; set; } = 0;

        [StringLength(300)]
        public string? InvoiceFooterNote { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;
    }
}
