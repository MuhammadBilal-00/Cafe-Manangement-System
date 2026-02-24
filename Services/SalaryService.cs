using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class SalaryService : ISalaryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;

        // Salary policy constants
        private const decimal LATE_THRESHOLD_DAYS = 3; // More than 3 late days = deduction
        private const decimal LATE_DEDUCTION_PER_DAY = 0.5m; // Half day deducted per late day over threshold
        private const decimal HALF_DAY_FACTOR = 0.5m;
        private const decimal PERFECT_ATTENDANCE_BONUS_PERCENT = 5m; // 5% bonus for 100% attendance

        public SalaryService(ApplicationDbContext context, IAttendanceService attendanceService)
        {
            _context = context;
            _attendanceService = attendanceService;
        }

        public async Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById)
        {
            var staffQuery = _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .Where(s => s.IsActive);

            if (branchId.HasValue)
                staffQuery = staffQuery.Where(s => s.BranchId == branchId.Value);

            var staffList = await staffQuery.ToListAsync();
            var results = new List<SalaryRecord>();

            foreach (var staff in staffList)
            {
                // Skip if salary already generated for this period
                var existing = await _context.SalaryRecords
                    .FirstOrDefaultAsync(sr => sr.StaffId == staff.Id && sr.Year == year && sr.Month == month);
                if (existing != null)
                {
                    results.Add(existing);
                    continue;
                }

                var stats = await _attendanceService.GetStaffMonthlyStatsAsync(staff.Id, year, month);
                var baseSalary = await CalculateBaseSalaryForStaff(staff.Id);

                // Calculate per-day rate
                decimal perDayRate = stats.totalWorkingDays > 0
                    ? baseSalary / stats.totalWorkingDays
                    : 0;

                // Effective present days: present + late + half-day(0.5)
                decimal effectiveDays = stats.daysPresent + stats.daysLate + (stats.daysHalfDay * HALF_DAY_FACTOR);

                // Earned salary based on attendance
                decimal earnedSalary = perDayRate * effectiveDays;

                // Deductions
                decimal deductionAmount = 0;
                string deductionReason = "";

                // Absence deduction (already factored in by earned salary)
                decimal absenceDeduction = baseSalary - (perDayRate * (stats.daysPresent + stats.daysLate + stats.daysHalfDay));
                if (stats.daysAbsent > 0)
                {
                    deductionReason += $"{stats.daysAbsent} absent day(s). ";
                }

                // Late deduction: if late days exceed threshold
                if (stats.daysLate > LATE_THRESHOLD_DAYS)
                {
                    int excessLateDays = stats.daysLate - (int)LATE_THRESHOLD_DAYS;
                    decimal lateDeduction = perDayRate * LATE_DEDUCTION_PER_DAY * excessLateDays;
                    deductionAmount += lateDeduction;
                    deductionReason += $"{excessLateDays} excess late day(s) deduction. ";
                }

                // Bonus
                decimal bonusAmount = 0;
                string bonusReason = "";

                // Perfect attendance bonus (100% present, no late, no absent, no half-day)
                if (stats.daysPresent == stats.totalWorkingDays && stats.daysAbsent == 0 &&
                    stats.daysLate == 0 && stats.daysHalfDay == 0 && stats.totalWorkingDays > 0)
                {
                    bonusAmount = baseSalary * (PERFECT_ATTENDANCE_BONUS_PERCENT / 100m);
                    bonusReason = "Perfect attendance bonus (100%).";
                }
                // High performance bonus from staff record
                else if (staff.PerformanceRating.HasValue && staff.PerformanceRating.Value == 5)
                {
                    bonusAmount = baseSalary * 0.03m; // 3% for top performance
                    bonusReason = "Outstanding performance bonus.";
                }

                decimal finalSalary = earnedSalary + bonusAmount - deductionAmount;
                if (finalSalary < 0) finalSalary = 0;

                var record = new SalaryRecord
                {
                    StaffId = staff.Id,
                    BranchId = staff.BranchId,
                    Year = year,
                    Month = month,
                    BaseSalary = baseSalary,
                    TotalWorkingDays = stats.totalWorkingDays,
                    DaysPresent = stats.daysPresent,
                    DaysAbsent = stats.daysAbsent,
                    DaysLate = stats.daysLate,
                    DaysHalfDay = stats.daysHalfDay,
                    AttendancePercentage = stats.percentage,
                    BonusAmount = Math.Round(bonusAmount, 2),
                    DeductionAmount = Math.Round(deductionAmount, 2),
                    BonusReason = string.IsNullOrEmpty(bonusReason) ? null : bonusReason.Trim(),
                    DeductionReason = string.IsNullOrEmpty(deductionReason) ? null : deductionReason.Trim(),
                    FinalSalary = Math.Round(finalSalary, 2),
                    PaymentStatus = "Pending",
                    GeneratedById = generatedById,
                    GeneratedAt = DateTime.Now
                };

                _context.SalaryRecords.Add(record);
                results.Add(record);
            }

            await _context.SaveChangesAsync();
            return results;
        }

        public async Task<SalaryRecord?> GetSalaryRecordAsync(int id)
        {
            return await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Include(sr => sr.GeneratedBy)
                .FirstOrDefaultAsync(sr => sr.Id == id);
        }

        public async Task<bool> MarkAsPaidAsync(int id)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.PaymentStatus == "Paid") return false;

            record.PaymentStatus = "Paid";
            record.PaidDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasSalariesGeneratedAsync(int year, int month, int? branchId)
        {
            var query = _context.SalaryRecords.Where(sr => sr.Year == year && sr.Month == month);
            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);

            return await query.AnyAsync();
        }

        public async Task<decimal> CalculateBaseSalaryForStaff(int staffId)
        {
            // First check StaffSalary records for active salary
            var activeSalary = await _context.StaffSalaries
                .Where(ss => ss.StaffId == staffId && ss.IsActive)
                .OrderByDescending(ss => ss.EffectiveFromDate)
                .FirstOrDefaultAsync();

            if (activeSalary != null && activeSalary.BaseSalary > 0)
                return activeSalary.BaseSalary;

            // Fallback to role default monthly salary
            var staff = await _context.Staff
                .Include(s => s.StaffRole)
                .FirstOrDefaultAsync(s => s.Id == staffId);

            if (staff?.StaffRole != null && staff.StaffRole.DefaultMonthlySalary > 0)
                return staff.StaffRole.DefaultMonthlySalary;

            // Last resort default
            return 30000m;
        }
    }
}
