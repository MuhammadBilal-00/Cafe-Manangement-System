using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class DailySpecial : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        public int BranchId { get; set; }

        public DateTime SpecialDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal SpecialPrice { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;
    }
}
