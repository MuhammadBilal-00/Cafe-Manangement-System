using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;
using Cafe.Models.ViewModels;

namespace Cafe.Services
{
    public interface IAttendanceService
    {
        // ── Constants ──
        const decimal StandardDailyHours = 8m;
        const int LateThresholdMinutes = 15;
        static readonly TimeSpan DefaultShiftStart = new(9, 0, 0);

        // ── Core Operations ──
        Task<Attendance> MarkAttendanceAsync(int staffId, int branchId, DateTime date,
            TimeSpan? clockIn, TimeSpan? clockOut, string? notes, int? markedById);

        Task<Attendance?> ClockOutAsync(int staffId, DateTime date, TimeSpan clockOut);

        Task<Attendance?> UpdateAttendanceAsync(int id, TimeSpan? clockIn, TimeSpan? clockOut, string? notes);

        Task<bool> HasAttendanceAsync(int staffId, DateTime date);

        Task<Attendance?> GetAttendanceAsync(int id);

        Task<Attendance?> GetTodayAttendanceAsync(int staffId);

        // ── Reporting ──
        Task<List<StaffAttendanceSummary>> GetMonthlySummaryAsync(int year, int month, int? branchId);

        Task<AttendanceStats> GetStaffMonthlyStatsAsync(int staffId, int year, int month);

        // ── Auto-calculation logic ──
        (string status, decimal totalHours, decimal overtimeHours, int lateMinutes) CalculateAttendanceFields(
            TimeSpan? clockIn, TimeSpan? clockOut, TimeSpan shiftStart);

        Task<TimeSpan> GetShiftStartForStaff(int staffId, DateTime date);
    }

    /// <summary>
    /// Detailed attendance statistics for a staff member in a given month.
    /// </summary>
    public class AttendanceStats
    {
        public int TotalWorkingDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public int DaysLate { get; set; }
        public int DaysHalfDay { get; set; }
        public decimal TotalOvertimeHours { get; set; }
        public decimal AttendancePercentage { get; set; }
    }
}
