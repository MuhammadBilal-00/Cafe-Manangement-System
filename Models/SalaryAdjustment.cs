using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class SalaryAdjustment : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int SalaryRecordId { get; set; }

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "Bonus"; // Bonus or Deduction

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("SalaryRecordId")]
        public SalaryRecord SalaryRecord { get; set; } = null!;

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }
    }
}
