using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// One tender against an invoice (Phase 1 split payments). An invoice can have several
    /// payments (e.g. Rs.500 Cash + Rs.300 Card); Invoice.PaymentStatus derives from
    /// sum(payments) vs the invoice total.
    /// </summary>
    public class Payment : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int InvoiceId { get; set; }

        /// <summary>Cash | Card | Wallet | BankTransfer | Terminal</summary>
        [Required]
        [StringLength(30)]
        public string Method { get; set; } = "Cash";

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        /// <summary>Optional tender reference (terminal txn id, cheque no., last-4, …).</summary>
        [StringLength(100)]
        public string? Reference { get; set; }

        public DateTime PaidAt { get; set; } = DateTime.Now;

        [ForeignKey("InvoiceId")]
        public Invoice Invoice { get; set; } = null!;
    }
}
