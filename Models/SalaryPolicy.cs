using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Versioned salary policy — exactly ONE active at a time.
    /// Past salaries freeze the PolicyId they were generated with.
    /// </summary>
    public class SalaryPolicy
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // ── Penalty / Deduction Rules ──

        /// <summary>Per-day deduction factor for absences (1.0 = full daily rate).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal AbsenceDeductionFactor { get; set; } = 1.0m;

        /// <summary>Per-day deduction factor for half-days (0.5 = half daily rate).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal HalfDayDeductionFactor { get; set; } = 0.5m;

        /// <summary>Number of late days that trigger one penalty unit.</summary>
        public int LatePenaltyThreshold { get; set; } = 3;

        /// <summary>Deduction factor per penalty unit (0.5 = half daily rate per unit).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal LatePenaltyFactor { get; set; } = 0.5m;

        // ── Overtime Rules ──

        [Column(TypeName = "decimal(5,2)")]
        public decimal OvertimeMultiplier { get; set; } = 1.5m;

        // ── Attendance Bonus Rules ──

        /// <summary>Bonus percentage of base salary for perfect/near-perfect attendance.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal AttendanceBonusPercentage { get; set; } = 5.0m;

        /// <summary>Max late days still eligible for attendance bonus.</summary>
        public int MaxLateForBonus { get; set; } = 2;

        /// <summary>Max absent days still eligible for attendance bonus (0 = must be perfect).</summary>
        public int MaxAbsentForBonus { get; set; } = 0;

        // ── Standard Hours ──

        [Column(TypeName = "decimal(5,2)")]
        public decimal StandardDailyHours { get; set; } = 8.0m;

        public int LateThresholdMinutes { get; set; } = 15;

        // ── Validity Period ──

        [Required]
        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;

        // ── Audit ──

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? UpdatedById { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // ── Navigation ──

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }

        [ForeignKey("UpdatedById")]
        public User? UpdatedBy { get; set; }
    }
}
