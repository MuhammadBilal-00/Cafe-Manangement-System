using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 5: a double-entry journal entry. Total debits must equal total credits (enforced in
    /// the service). Auto-posted entries carry SourceType+SourceId, unique per tenant for idempotency.
    /// </summary>
    public class JournalEntry : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        public int? BranchId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(60)] public string? Reference { get; set; }
        [StringLength(300)] public string? Memo { get; set; }

        /// <summary>Manual | Invoice | Expense | Purchase | Payroll | SellReturn | PurchaseReturn</summary>
        [Required][StringLength(30)] public string SourceType { get; set; } = "Manual";
        public int? SourceId { get; set; }

        /// <summary>Draft | Posted | Void</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Posted";

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

        [NotMapped] public decimal TotalDebit => Lines.Sum(l => l.Debit);
        [NotMapped] public decimal TotalCredit => Lines.Sum(l => l.Credit);
    }

    public class JournalLine : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int JournalEntryId { get; set; }
        [Required] public int AccountId { get; set; }

        [Column(TypeName = "decimal(14,2)")] public decimal Debit { get; set; }
        [Column(TypeName = "decimal(14,2)")] public decimal Credit { get; set; }
        [StringLength(200)] public string? Description { get; set; }

        [ForeignKey("JournalEntryId")] public JournalEntry? JournalEntry { get; set; }
        [ForeignKey("AccountId")] public Account? Account { get; set; }
    }
}
