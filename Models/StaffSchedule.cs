using Cafe.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class StaffSchedule : ITenantOwned
{
    public int Id { get; set; }
    // ── Multi-tenant isolation (Phase 0) ──
    public int TenantId { get; set; }

    [Required]
    public int StaffId { get; set; }

    [Required]
    public DateTime ShiftDate { get; set; }

    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }

    [StringLength(20)]
    public string ShiftType { get; set; } = "Regular"; // Regular, Overtime, Holiday

    [Range(0, 24)]
    public decimal HoursWorked { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsApproved { get; set; } = false;
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Navigation Properties
    [ForeignKey("StaffId")]
    public Staff Staff { get; set; } = null!;

    [ForeignKey("ApprovedBy")]
    public User? ApprovedByUser { get; set; }
}
