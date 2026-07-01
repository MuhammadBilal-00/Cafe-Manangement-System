using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 5: a bank or cash register, linked to a CoA account, with a reconciled balance.</summary>
    public class PaymentAccount : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(80)] public string Name { get; set; } = string.Empty;
        /// <summary>Bank | Cash | Wallet</summary>
        [Required][StringLength(20)] public string Type { get; set; } = "Cash";

        /// <summary>The CoA (Asset) account this register posts to.</summary>
        public int? AccountId { get; set; }

        [Column(TypeName = "decimal(14,2)")] public decimal OpeningBalance { get; set; }
        /// <summary>Last reconciled statement balance.</summary>
        [Column(TypeName = "decimal(14,2)")] public decimal ReconciledBalance { get; set; }
        public DateTime? LastReconciledAt { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey("AccountId")] public Account? Account { get; set; }
    }
}
