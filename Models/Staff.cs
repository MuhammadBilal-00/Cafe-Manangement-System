using Cafe.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Staff : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int StaffRoleId { get; set; } // Changed from Role string to RoleId

        [Required]
        public int BranchId { get; set; }

        public DateTime HireDate { get; set; } = DateTime.Now;
        public DateTime? TerminationDate { get; set; }

        [StringLength(20)]
        public string EmploymentStatus { get; set; } = "Active"; // Active, Terminated, Suspended

        [StringLength(20)]
        public string EmploymentType { get; set; } = "Full-time"; // Full-time, Part-time, Contract

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? EmployeeId { get; set; } // Unique employee identifier

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Performance tracking
        [Range(1, 5)]
        public int? PerformanceRating { get; set; }

        public DateTime? LastPerformanceReview { get; set; }

        // Navigation Properties
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [ForeignKey("StaffRoleId")]
        public StaffRole StaffRole { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        // Collections
        public ICollection<StaffSalary> SalaryHistory { get; set; } = new List<StaffSalary>();
        public ICollection<StaffSchedule> Schedules { get; set; } = new List<StaffSchedule>();
    }
}