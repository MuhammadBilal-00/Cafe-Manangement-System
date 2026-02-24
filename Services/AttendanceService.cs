using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> MarkAttendanceAsync(int staffId, int branchId, DateTime date, string status,
            TimeSpan? checkIn, TimeSpan? checkOut, int lateMinutes, string? notes, int? markedById)
        {
            // Prevent duplicates
            if (await HasAttendanceAsync(staffId, date))
                return false;

            var attendance = new Attendance
            {
                StaffId = staffId,
                BranchId = branchId,
                Date = date.Date,
                Status = status,
                CheckInTime = checkIn,
                CheckOutTime = checkOut,
                LateMinutes = status == "Late" ? lateMinutes : 0,
                Notes = notes,
                MarkedById = markedById,
                CreatedAt = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateAttendanceAsync(int id, string status, TimeSpan? checkIn, TimeSpan? checkOut,
            int lateMinutes, string? notes)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record == null) return false;

            record.Status = status;
            record.CheckInTime = checkIn;
            record.CheckOutTime = checkOut;
            record.LateMinutes = status == "Late" ? lateMinutes : 0;
            record.Notes = notes;
            record.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasAttendanceAsync(int staffId, DateTime date)
        {
            return await _context.Attendances
                .AnyAsync(a => a.StaffId == staffId && a.Date == date.Date);
        }

        public async Task<Attendance?> GetAttendanceAsync(int id)
        {
            return await _context.Attendances
                .Include(a => a.Staff).ThenInclude(s => s.User)
                .Include(a => a.Staff).ThenInclude(s => s.StaffRole)
                .Include(a => a.Branch)
                .Include(a => a.MarkedBy)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<StaffAttendanceSummary>> GetMonthlySummaryAsync(int year, int month, int? branchId)
        {
            var staffQuery = _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .Where(s => s.IsActive);

            if (branchId.HasValue)
                staffQuery = staffQuery.Where(s => s.BranchId == branchId.Value);

            var staffList = await staffQuery.ToListAsync();
            var summaries = new List<StaffAttendanceSummary>();

            foreach (var staff in staffList)
            {
                var stats = await GetStaffMonthlyStatsAsync(staff.Id, year, month);
                summaries.Add(new StaffAttendanceSummary
                {
                    StaffId = staff.Id,
                    StaffName = staff.User?.Name ?? "Unknown",
                    BranchName = staff.Branch?.Name ?? "Unknown",
                    Role = staff.StaffRole?.RoleName ?? "Unknown",
                    TotalWorkingDays = stats.totalWorkingDays,
                    DaysPresent = stats.daysPresent,
                    DaysAbsent = stats.daysAbsent,
                    DaysLate = stats.daysLate,
                    DaysHalfDay = stats.daysHalfDay,
                    AttendancePercentage = stats.percentage
                });
            }

            return summaries;
        }

        public async Task<(int totalWorkingDays, int daysPresent, int daysAbsent, int daysLate, int daysHalfDay, decimal percentage)>
            GetStaffMonthlyStatsAsync(int staffId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            // Calculate working days (Mon-Sat)
            int totalWorkingDays = 0;
            for (var d = startDate; d < endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Sunday)
                    totalWorkingDays++;
            }

            // If future month, cap working days to today
            if (endDate > DateTime.Today.AddDays(1))
            {
                totalWorkingDays = 0;
                var cap = DateTime.Today < endDate ? DateTime.Today : endDate.AddDays(-1);
                for (var d = startDate; d <= cap; d = d.AddDays(1))
                {
                    if (d.DayOfWeek != DayOfWeek.Sunday)
                        totalWorkingDays++;
                }
            }

            var records = await _context.Attendances
                .Where(a => a.StaffId == staffId && a.Date >= startDate && a.Date < endDate)
                .ToListAsync();

            int daysPresent = records.Count(r => r.Status == "Present");
            int daysLate = records.Count(r => r.Status == "Late");
            int daysHalfDay = records.Count(r => r.Status == "Half-Day");
            int daysAbsent = records.Count(r => r.Status == "Absent");

            // Days not marked at all count as absent (past working days only)
            int markedDays = records.Count;
            int unmarkedWorkingDays = totalWorkingDays - markedDays;
            if (unmarkedWorkingDays > 0)
                daysAbsent += unmarkedWorkingDays;

            // Present + Late + Half(0.5) count toward attendance
            decimal effectiveDays = daysPresent + daysLate + (daysHalfDay * 0.5m);
            decimal percentage = totalWorkingDays > 0
                ? Math.Round((effectiveDays / totalWorkingDays) * 100, 2)
                : 0;

            return (totalWorkingDays, daysPresent, daysAbsent, daysLate, daysHalfDay, percentage);
        }
    }
}
