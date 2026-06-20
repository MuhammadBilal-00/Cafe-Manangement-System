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

        // 
        //  CORE OPERATIONS
        // 

        public async Task<Attendance> MarkAttendanceAsync(int staffId, int branchId, DateTime date,
            TimeSpan? clockIn, TimeSpan? clockOut, string? notes, int? markedById,
            string? manualStatus = null)
        {
            if (await HasAttendanceAsync(staffId, date))
                throw new InvalidOperationException("Attendance already marked for this staff member on this date.");

            string status;
            decimal totalHours = 0m, overtimeHours = 0m;
            int lateMinutes = 0;

            // Manual status overrides auto-calculation (used for leave types, holidays, WFH)
            if (!string.IsNullOrWhiteSpace(manualStatus) && IsLeaveOrSpecialStatus(manualStatus))
            {
                status = manualStatus;
                // WFH still tracks hours if times provided
                if (manualStatus == "Work From Home" && clockIn.HasValue && clockOut.HasValue)
                {
                    totalHours = Math.Max(0, Math.Round((decimal)(clockOut.Value - clockIn.Value).TotalHours, 2));
                    overtimeHours = totalHours > IAttendanceService.StandardDailyHours
                        ? Math.Round(totalHours - IAttendanceService.StandardDailyHours, 2)
                        : 0m;
                }
            }
            else
            {
                var shiftStart = await GetShiftStartForStaff(staffId, date);
                (status, totalHours, overtimeHours, lateMinutes) =
                    CalculateAttendanceFields(clockIn, clockOut, shiftStart);

                if (!clockIn.HasValue && !clockOut.HasValue)
                {
                    status = "Absent";
                    totalHours = 0;
                    overtimeHours = 0;
                    lateMinutes = 0;
                }

                // Allow manual override for non-leave statuses too (e.g. marking "Overtime" explicitly)
                if (!string.IsNullOrWhiteSpace(manualStatus) && manualStatus == "Overtime" && overtimeHours > 0)
                    status = "Overtime";
            }

            var attendance = new Attendance
            {
                StaffId = staffId,
                BranchId = branchId,
                Date = date.Date,
                CheckInTime = clockIn,
                CheckOutTime = clockOut,
                Status = status,
                TotalHours = totalHours,
                OvertimeHours = overtimeHours,
                LateMinutes = lateMinutes,
                Notes = notes,
                MarkedById = markedById,
                CreatedAt = DateTime.Now
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
            return attendance;
        }

        private static bool IsLeaveOrSpecialStatus(string status)
            => status is "Paid Leave" or "Sick Leave" or "Casual Leave" or "Holiday" or "Work From Home";

        public async Task<Attendance?> ClockOutAsync(int staffId, DateTime date, TimeSpan clockOut)
        {
            var record = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date == date.Date);

            if (record == null) return null;

            record.CheckOutTime = clockOut;
            record.UpdatedAt = DateTime.Now;

            var shiftStart = await GetShiftStartForStaff(staffId, date);
            var (status, totalHours, overtimeHours, lateMinutes) =
                CalculateAttendanceFields(record.CheckInTime, clockOut, shiftStart);

            record.Status = status;
            record.TotalHours = totalHours;
            record.OvertimeHours = overtimeHours;
            record.LateMinutes = lateMinutes;

            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<Attendance?> UpdateAttendanceAsync(int id, TimeSpan? clockIn, TimeSpan? clockOut, string? notes,
            string? manualStatus = null)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record == null) return null;

            record.CheckInTime = clockIn;
            record.CheckOutTime = clockOut;
            record.Notes = notes;
            record.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(manualStatus) && IsLeaveOrSpecialStatus(manualStatus))
            {
                record.Status = manualStatus;
                record.TotalHours = 0;
                record.OvertimeHours = 0;
                record.LateMinutes = 0;

                if (manualStatus == "Work From Home" && clockIn.HasValue && clockOut.HasValue)
                {
                    record.TotalHours = Math.Max(0, Math.Round((decimal)(clockOut.Value - clockIn.Value).TotalHours, 2));
                    record.OvertimeHours = record.TotalHours > IAttendanceService.StandardDailyHours
                        ? Math.Round(record.TotalHours - IAttendanceService.StandardDailyHours, 2)
                        : 0m;
                }
            }
            else if (!clockIn.HasValue && !clockOut.HasValue)
            {
                record.Status = "Absent";
                record.TotalHours = 0;
                record.OvertimeHours = 0;
                record.LateMinutes = 0;
            }
            else
            {
                var shiftStart = await GetShiftStartForStaff(record.StaffId, record.Date);
                var (status, totalHours, overtimeHours, lateMinutes) =
                    CalculateAttendanceFields(clockIn, clockOut, shiftStart);

                record.Status = status;
                record.TotalHours = totalHours;
                record.OvertimeHours = overtimeHours;
                record.LateMinutes = lateMinutes;
            }

            await _context.SaveChangesAsync();
            return record;
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

        public async Task<Attendance?> GetTodayAttendanceAsync(int staffId)
        {
            return await _context.Attendances
                .FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date == DateTime.Today);
        }

        // 
        //  REPORTING
        // 

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
                    TotalWorkingDays = stats.TotalWorkingDays,
                    DaysPresent = stats.DaysPresent,
                    DaysAbsent = stats.DaysAbsent,
                    DaysLate = stats.DaysLate,
                    DaysHalfDay = stats.DaysHalfDay,
                    DaysPaidLeave = stats.DaysPaidLeave,
                    DaysSickLeave = stats.DaysSickLeave,
                    DaysCasualLeave = stats.DaysCasualLeave,
                    DaysHoliday = stats.DaysHoliday,
                    DaysWFH = stats.DaysWFH,
                    TotalOvertimeHours = stats.TotalOvertimeHours,
                    AttendancePercentage = stats.AttendancePercentage
                });
            }

            return summaries;
        }

        public async Task<AttendanceStats> GetStaffMonthlyStatsAsync(int staffId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            // Working days (Mon-Sat), capped to today for current/future months
            int totalWorkingDays = 0;
            var capDate = endDate > DateTime.Today.AddDays(1) ? DateTime.Today : endDate.AddDays(-1);
            for (var d = startDate; d <= capDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Sunday)
                    totalWorkingDays++;
            }

            var records = await _context.Attendances
                .Where(a => a.StaffId == staffId && a.Date >= startDate && a.Date < endDate)
                .ToListAsync();

            int daysPresent  = records.Count(r => r.Status == "Present");
            int daysLate     = records.Count(r => r.Status == "Late");
            int daysHalfDay  = records.Count(r => r.Status == "Half-Day");
            int daysAbsent   = records.Count(r => r.Status == "Absent");
            int daysPaidLeave   = records.Count(r => r.Status == "Paid Leave");
            int daysSickLeave   = records.Count(r => r.Status == "Sick Leave");
            int daysCasualLeave = records.Count(r => r.Status == "Casual Leave");
            int daysHoliday     = records.Count(r => r.Status == "Holiday");
            int daysWFH         = records.Count(r => r.Status == "Work From Home");
            int daysOvertimeStatus = records.Count(r => r.Status == "Overtime");

            // Only truly unaccounted working days count as absent
            int accountedDays = records.Count;
            int unmarkedWorkingDays = totalWorkingDays - accountedDays;
            if (unmarkedWorkingDays > 0)
                daysAbsent += unmarkedWorkingDays;

            decimal totalOvertimeHours = records.Sum(r => r.OvertimeHours);

            // Effective presence: Present + Late + HalfDay(0.5) + WFH + PaidLeave + Holiday + Overtime
            decimal effectiveDays = daysPresent + daysLate + (daysHalfDay * 0.5m)
                + daysWFH + daysPaidLeave + daysHoliday + daysOvertimeStatus;

            decimal percentage = totalWorkingDays > 0
                ? Math.Round((effectiveDays / totalWorkingDays) * 100, 2)
                : 0;

            return new AttendanceStats
            {
                TotalWorkingDays = totalWorkingDays,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                DaysLate = daysLate,
                DaysHalfDay = daysHalfDay,
                DaysPaidLeave = daysPaidLeave,
                DaysSickLeave = daysSickLeave,
                DaysCasualLeave = daysCasualLeave,
                DaysHoliday = daysHoliday,
                DaysWFH = daysWFH,
                DaysOvertime = daysOvertimeStatus,
                TotalOvertimeHours = totalOvertimeHours,
                AttendancePercentage = percentage
            };
        }

        // 
        //  AUTO-CALCULATION ENGINE
        // 

        public (string status, decimal totalHours, decimal overtimeHours, int lateMinutes) CalculateAttendanceFields(
            TimeSpan? clockIn, TimeSpan? clockOut, TimeSpan shiftStart)
        {
            // Only clock-in (no clock-out yet)  tentatively present or late
            if (clockIn.HasValue && !clockOut.HasValue)
            {
                int late = 0;
                string s = "Present";
                if (clockIn.Value > shiftStart.Add(TimeSpan.FromMinutes(IAttendanceService.LateThresholdMinutes)))
                {
                    late = (int)(clockIn.Value - shiftStart).TotalMinutes;
                    s = "Late";
                }
                return (s, 0m, 0m, late);
            }

            if (!clockIn.HasValue || !clockOut.HasValue)
                return ("Absent", 0m, 0m, 0);

            decimal totalHours = Math.Max(0, Math.Round((decimal)(clockOut.Value - clockIn.Value).TotalHours, 2));

            decimal overtimeHours = totalHours > IAttendanceService.StandardDailyHours
                ? Math.Round(totalHours - IAttendanceService.StandardDailyHours, 2)
                : 0m;

            int lateMinutes = 0;
            bool isLate = false;
            if (clockIn.Value > shiftStart.Add(TimeSpan.FromMinutes(IAttendanceService.LateThresholdMinutes)))
            {
                lateMinutes = (int)(clockIn.Value - shiftStart).TotalMinutes;
                isLate = true;
            }

            string status;
            if (totalHours >= IAttendanceService.StandardDailyHours)
                status = isLate ? "Late" : "Present";
            else if (totalHours >= 4m)
                status = "Half-Day";
            else
                status = "Absent";

            return (status, totalHours, overtimeHours, lateMinutes);
        }

        public async Task<TimeSpan> GetShiftStartForStaff(int staffId, DateTime date)
        {
            var schedule = await _context.StaffSchedules
                .Where(ss => ss.StaffId == staffId && ss.ShiftDate.Date == date.Date)
                .FirstOrDefaultAsync();

            return schedule?.ShiftStartTime ?? IAttendanceService.DefaultShiftStart;
        }
    }
}
