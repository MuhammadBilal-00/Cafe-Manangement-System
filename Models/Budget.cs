using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 5: a budget for a period, with a target amount per account.</summary>
    public class Budget : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(80)] public string Name { get; set; } = string.Empty;
        public int Year { get; set; } = DateTime.Now.Year;
        public int? BranchId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<BudgetLine> Lines { get; set; } = new List<BudgetLine>();
    }

    public class BudgetLine : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BudgetId { get; set; }
        [Required] public int AccountId { get; set; }
        [Column(TypeName = "decimal(14,2)")] public decimal Amount { get; set; }

        [ForeignKey("BudgetId")] public Budget? Budget { get; set; }
        [ForeignKey("AccountId")] public Account? Account { get; set; }
    }
}
