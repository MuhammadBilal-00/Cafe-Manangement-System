using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 6: a prepaid gift card / voucher usable as a payment method. Balance changes are atomic.</summary>
    public class GiftCard : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(30)] public string Code { get; set; } = string.Empty;
        [Column(TypeName = "decimal(10,2)")] public decimal InitialBalance { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal Balance { get; set; }

        public int? CustomerUserId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<GiftCardTransaction> Transactions { get; set; } = new List<GiftCardTransaction>();
    }

    public class GiftCardTransaction : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int GiftCardId { get; set; }
        /// <summary>Signed: + top-up/issue, − redeem.</summary>
        [Column(TypeName = "decimal(10,2)")] public decimal Amount { get; set; }
        public int? InvoiceId { get; set; }
        [StringLength(200)] public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("GiftCardId")] public GiftCard? GiftCard { get; set; }
    }
}
