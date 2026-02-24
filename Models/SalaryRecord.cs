using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class SalaryRecord
    {
        public int Id { get; set; }

        [Required]
        public int StaffId { get; set; }

        [Required]
        public int BranchId { get; set; }

        // Period
        [Required]
        public int Year { get; set; }

        [Required]
        [Range(1, 12)]
        public int Month { get; set; }

        // Salary Breakdown
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseSalary { get; set; }

        public int TotalWorkingDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public int DaysLate { get; set; }
        public int DaysHalfDay { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal AttendancePercentage { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal BonusAmount { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal DeductionAmount { get; set; } = 0;

        [StringLength(500)]
        public string? BonusReason { get; set; }

        [StringLength(500)]
        public string? DeductionReason { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal FinalSalary { get; set; }

        [Required]
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Cancelled

        public DateTime? PaidDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? GeneratedById { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("StaffId")]
        public Staff Staff { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("GeneratedById")]
        public User? GeneratedBy { get; set; }
    }
}
