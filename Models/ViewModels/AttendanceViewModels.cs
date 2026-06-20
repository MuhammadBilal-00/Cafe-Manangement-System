using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class AttendanceIndexViewModel
    {
        public List<Attendance> Records { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();
        public List<Staff> StaffList { get; set; } = new();

        // Filters
        public int? BranchId { get; set; }
        public int? StaffId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Status { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; } = 25;

        // Summary stats
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalLate { get; set; }
        public int TotalHalfDay { get; set; }
        public int TotalPaidLeave { get; set; }
        public int TotalSickLeave { get; set; }
        public int TotalCasualLeave { get; set; }
        public int TotalHoliday { get; set; }
        public int TotalWFH { get; set; }
        public int TotalOvertime { get; set; }

        // Today's snapshot (for dashboard KPIs)
        public int TodayCheckedIn { get; set; }
        public int TodayAbsent { get; set; }
        public int TodayOnLeave { get; set; }
        public int TodayLate { get; set; }
        public int TodayTotalStaff { get; set; }
    }

    public class AttendanceMarkViewModel
    {
        public int StaffId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
        public string Status { get; set; } = "Auto";
        public string? ManualStatus { get; set; }
        public int LateMinutes { get; set; } = 0;
        public string? Notes { get; set; }
    }

    public class AttendanceSummaryViewModel
    {
        public List<StaffAttendanceSummary> Summaries { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();
        public int? BranchId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;
    }

    public class StaffAttendanceSummary
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int TotalWorkingDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public int DaysLate { get; set; }
        public int DaysHalfDay { get; set; }
        public int DaysPaidLeave { get; set; }
        public int DaysSickLeave { get; set; }
        public int DaysCasualLeave { get; set; }
        public int DaysHoliday { get; set; }
        public int DaysWFH { get; set; }
        public decimal TotalOvertimeHours { get; set; }
        public decimal AttendancePercentage { get; set; }
    }

    public class BulkAttendanceViewModel
    {
        public int BranchId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public List<BulkAttendanceEntry> Entries { get; set; } = new();
    }

    public class BulkAttendanceEntry
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Status { get; set; } = "Auto";
        public string? ManualStatus { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
        public int LateMinutes { get; set; } = 0;
        public string? Notes { get; set; }
        public bool AlreadyMarked { get; set; } = false;
        public string? ExistingStatus { get; set; }
    }
}
