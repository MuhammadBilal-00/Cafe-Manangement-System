using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 5: a chart-of-accounts node (double-entry). Hierarchical via ParentId. Normal balance
    /// side is implied by Type: Asset/Expense are debit-normal; Liability/Equity/Income are credit-normal.
    /// </summary>
    public class Account : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(20)] public string Code { get; set; } = string.Empty;   // e.g. "1000"
        [Required][StringLength(120)] public string Name { get; set; } = string.Empty;

        /// <summary>Asset | Liability | Equity | Income | Expense</summary>
        [Required][StringLength(20)] public string Type { get; set; } = "Asset";

        public int? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>System accounts are created by seeding and referenced by auto-posting.</summary>
        public bool IsSystem { get; set; } = false;

        [ForeignKey("ParentId")] public Account? Parent { get; set; }
    }
}
