using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;
using Cafe.Models.ViewModels;

namespace Cafe.Services
{
    public interface IAttendanceService
    {
        Task<bool> MarkAttendanceAsync(int staffId, int branchId, DateTime date, string status,
            TimeSpan? checkIn, TimeSpan? checkOut, int lateMinutes, string? notes, int? markedById);
        Task<bool> UpdateAttendanceAsync(int id, string status, TimeSpan? checkIn, TimeSpan? checkOut,
            int lateMinutes, string? notes);
        Task<bool> HasAttendanceAsync(int staffId, DateTime date);
        Task<Attendance?> GetAttendanceAsync(int id);
        Task<List<StaffAttendanceSummary>> GetMonthlySummaryAsync(int year, int month, int? branchId);
        Task<(int totalWorkingDays, int daysPresent, int daysAbsent, int daysLate, int daysHalfDay, decimal percentage)>
            GetStaffMonthlyStatsAsync(int staffId, int year, int month);
    }
}
