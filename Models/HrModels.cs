using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    // ── 48: Departments & designations ──
    public class Department : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(80)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class Designation : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(80)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    // ── 46: Leave management ──
    public class LeaveType : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(60)] public string Name { get; set; } = string.Empty; // Paid/Sick/Casual …
        public int DaysPerYear { get; set; } = 0;
        public bool IsPaid { get; set; } = true;
        /// <summary>Attendance status stamped for approved days (must be one of the CK_Attendance_Status values).</summary>
        [Required][StringLength(30)] public string AttendanceStatus { get; set; } = "Paid Leave";
        public bool IsActive { get; set; } = true;
    }

    public class LeaveRequest : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required] public int StaffId { get; set; }
        [Required] public int LeaveTypeId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Days { get; set; }
        [StringLength(300)] public string? Reason { get; set; }
        /// <summary>Pending | Approved | Rejected</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Pending";
        public int? ApprovedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("StaffId")] public Staff? Staff { get; set; }
        [ForeignKey("LeaveTypeId")] public LeaveType? LeaveType { get; set; }
    }

    // ── 47: Holidays ──
    public class Holiday : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(100)] public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int? BranchId { get; set; }
        public bool IsRecurring { get; set; } = false;
    }

    // ── 49: Sales targets / commission ──
    public class SalesTarget : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? StaffId { get; set; }
        public int? BranchId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
        [Column(TypeName = "decimal(12,2)")] public decimal TargetAmount { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal CommissionPercent { get; set; }

        [ForeignKey("StaffId")] public Staff? Staff { get; set; }
    }

    // ── 50: Employee documents ──
    public class EmployeeDocument : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required] public int StaffId { get; set; }
        [Required][StringLength(120)] public string Title { get; set; } = string.Empty;
        [StringLength(40)] public string? DocType { get; set; }
        [StringLength(400)] public string? FileUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [ForeignKey("StaffId")] public Staff? Staff { get; set; }
    }
}
