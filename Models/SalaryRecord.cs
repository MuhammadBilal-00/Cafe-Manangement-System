using System;
using System.Collections.Generic;
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

        // ── Policy Snapshot (frozen at generation time) ──
        public int? PolicyIdUsed { get; set; }

        // ── Attendance Stats ──
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

        [Column(TypeName = "decimal(5,2)")]
        public decimal OvertimeHours { get; set; } = 0;

        // ── Earnings Breakdown ──
        [Column(TypeName = "decimal(10,2)")]
        public decimal OvertimePay { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal AttendanceBonus { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal GrossSalary { get; set; } = 0;

        // ── Deductions Breakdown ──
        [Column(TypeName = "decimal(10,2)")]
        public decimal AbsenceDeduction { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal HalfDayDeduction { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal LatePenaltyDeduction { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalDeductions { get; set; } = 0;

        // ── Legacy / Aggregate Fields (kept for compatibility) ──
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

        /// <summary>
        /// Alias for FinalSalary — the net payable amount after all deductions.
        /// </summary>
        [NotMapped]
        public decimal NetSalary => FinalSalary;

        // ── Workflow ──
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Finalized, Paid

        [Required]
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Cancelled

        public DateTime? PaidDate { get; set; }

        // Payment Details
        [StringLength(30)]
        public string? PaymentMethod { get; set; }   // Cash, Bank Transfer, Mobile Wallet, Cheque

        [StringLength(100)]
        public string? PaymentReference { get; set; } // Transaction ID / Cheque No.

        [StringLength(300)]
        public string? PaymentNotes { get; set; }

        public int? FinalizedById { get; set; }
        public DateTime? FinalizedAt { get; set; }
        public int? UnlockedById { get; set; }
        public DateTime? UnlockedAt { get; set; }

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

        [ForeignKey("PolicyIdUsed")]
        public SalaryPolicy? PolicyUsed { get; set; }

        [ForeignKey("FinalizedById")]
        public User? FinalizedBy { get; set; }

        [ForeignKey("UnlockedById")]
        public User? UnlockedBy { get; set; }

        // Adjustments
        public ICollection<SalaryAdjustment> Adjustments { get; set; } = new List<SalaryAdjustment>();
    }
}
