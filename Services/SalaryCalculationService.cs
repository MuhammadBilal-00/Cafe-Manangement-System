using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class SalaryCalculationService : ISalaryCalculationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;

        public SalaryCalculationService(ApplicationDbContext context, IAttendanceService attendanceService)
        {
            _context = context;
            _attendanceService = attendanceService;
        }

        // ────────────────────────────────────────────────────────
        //  Full async calculation (fetches policy + base + stats)
        // ────────────────────────────────────────────────────────

        public async Task<SalaryRecord> CalculateSalaryAsync(Staff staff, int year, int month, int? generatedById)
        {
            var baseSalary = await GetEffectiveBaseSalaryAsync(staff.Id, year, month);
            if (baseSalary <= 0)
                throw new InvalidOperationException(
                    $"No base salary is configured for {staff.User?.Name ?? $"staff #{staff.Id}"} — set one on the staff profile or the role before running payroll.");

            var policy = await GetEffectivePolicyAsync(year, month);
            var stats = await _attendanceService.GetStaffMonthlyStatsAsync(staff.Id, year, month);

            if (policy == null)
                throw new InvalidOperationException(
                    $"No active salary policy found for {year}-{month:D2}. Please create a salary policy first.");

            return CalculateSalary(staff, baseSalary, stats, policy, year, month, generatedById);
        }

        // ────────────────────────────────────────────────────────
        //  Pure calculation — no DB reads
        // ────────────────────────────────────────────────────────

        public SalaryRecord CalculateSalary(Staff staff, decimal baseSalary, AttendanceStats stats,
            SalaryPolicy policy, int year, int month, int? generatedById)
        {
            int workingDays = stats.TotalWorkingDays;
            if (workingDays <= 0) workingDays = 1; // safety

            // ── Rates ──
            decimal dailyRate = baseSalary / workingDays;
            decimal hourlyRate = dailyRate / policy.StandardDailyHours;

            // ── Deductions (from policy factors) ──
            // Sick Leave and Casual Leave are unpaid leave — deduct like absence
            // Paid Leave and Holiday are NOT deducted (already paid)
            int unpaidLeaveDays = stats.DaysSickLeave + stats.DaysCasualLeave;
            decimal absenceDeduction = (stats.DaysAbsent + unpaidLeaveDays) * dailyRate * policy.AbsenceDeductionFactor;
            decimal halfDayDeduction = stats.DaysHalfDay * dailyRate * policy.HalfDayDeductionFactor;

            int latePenaltyUnits = policy.LatePenaltyThreshold > 0
                ? stats.DaysLate / policy.LatePenaltyThreshold
                : 0;
            decimal latePenaltyDeduction = latePenaltyUnits * dailyRate * policy.LatePenaltyFactor;

            decimal totalDeductions = absenceDeduction + halfDayDeduction + latePenaltyDeduction;

            // ── Overtime ──
            decimal overtimePay = stats.TotalOvertimeHours * hourlyRate * policy.OvertimeMultiplier;

            // ── Attendance Bonus ──
            decimal attendanceBonus = 0;
            if (stats.DaysAbsent <= policy.MaxAbsentForBonus && stats.DaysLate <= policy.MaxLateForBonus)
            {
                attendanceBonus = baseSalary * (policy.AttendanceBonusPercentage / 100m);
            }

            // ── Gross & Net ──
            decimal grossSalary = baseSalary + overtimePay + attendanceBonus;
            decimal finalSalary = Math.Max(0, grossSalary - totalDeductions);

            // ── Reason strings ──
            var bonusReasons = new List<string>();
            if (attendanceBonus > 0)
                bonusReasons.Add($"Attendance bonus ({policy.AttendanceBonusPercentage}%)");

            var deductionReasons = new List<string>();
            if (stats.DaysAbsent > 0)
                deductionReasons.Add($"{stats.DaysAbsent} absent day(s)");
            if (stats.DaysHalfDay > 0)
                deductionReasons.Add($"{stats.DaysHalfDay} half-day(s)");
            if (latePenaltyUnits > 0)
                deductionReasons.Add($"{stats.DaysLate} late(s) = {latePenaltyUnits} penalty unit(s)");

            return new SalaryRecord
            {
                StaffId = staff.Id,
                BranchId = staff.BranchId,
                Year = year,
                Month = month,
                PolicyIdUsed = policy.Id,
                BaseSalary = baseSalary,
                TotalWorkingDays = stats.TotalWorkingDays,
                DaysPresent = stats.DaysPresent,
                DaysAbsent = stats.DaysAbsent,
                DaysLate = stats.DaysLate,
                DaysHalfDay = stats.DaysHalfDay,
                AttendancePercentage = stats.AttendancePercentage,
                OvertimeHours = stats.TotalOvertimeHours,
                OvertimePay = Math.Round(overtimePay, 2),
                AttendanceBonus = Math.Round(attendanceBonus, 2),
                AbsenceDeduction = Math.Round(absenceDeduction, 2),
                HalfDayDeduction = Math.Round(halfDayDeduction, 2),
                LatePenaltyDeduction = Math.Round(latePenaltyDeduction, 2),
                GrossSalary = Math.Round(grossSalary, 2),
                TotalDeductions = Math.Round(totalDeductions, 2),
                BonusAmount = Math.Round(attendanceBonus + overtimePay, 2),
                DeductionAmount = Math.Round(totalDeductions, 2),
                BonusReason = bonusReasons.Any() ? string.Join("; ", bonusReasons) : null,
                DeductionReason = deductionReasons.Any() ? string.Join("; ", deductionReasons) : null,
                FinalSalary = Math.Round(finalSalary, 2),
                Status = "Draft",
                PaymentStatus = "Pending",
                GeneratedById = generatedById,
                GeneratedAt = DateTime.Now
            };
        }

        // ────────────────────────────────────────────────────────
        //  Base Salary Lookup (historical)
        // ────────────────────────────────────────────────────────

        public async Task<decimal> GetEffectiveBaseSalaryAsync(int staffId, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            // Find the salary record that overlaps with the target month
            var salaryRecord = await _context.StaffSalaries
                .Where(ss => ss.StaffId == staffId
                    && ss.EffectiveFromDate <= monthEnd
                    && (ss.EffectiveToDate == null || ss.EffectiveToDate >= monthStart))
                .OrderByDescending(ss => ss.EffectiveFromDate)
                .FirstOrDefaultAsync();

            if (salaryRecord != null && salaryRecord.BaseSalary > 0)
                return salaryRecord.BaseSalary;

            // Fallback: role default salary
            var staff = await _context.Staff
                .Include(s => s.StaffRole)
                .FirstOrDefaultAsync(s => s.Id == staffId);

            if (staff?.StaffRole != null && staff.StaffRole.DefaultMonthlySalary > 0)
                return staff.StaffRole.DefaultMonthlySalary;

            // No salary record and no role default: 0 = "not configured". Payroll must never
            // invent a number — callers skip (and report) this staff member instead.
            return 0m;
        }

        // ────────────────────────────────────────────────────────
        //  Policy Lookup
        // ────────────────────────────────────────────────────────

        public async Task<SalaryPolicy?> GetEffectivePolicyAsync(int year, int month)
        {
            var targetDate = new DateTime(year, month, 1);

            return await _context.SalaryPolicies
                .Where(p => p.IsActive
                    && p.EffectiveFrom <= targetDate
                    && (p.EffectiveTo == null || p.EffectiveTo >= targetDate))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
        }
    }
}
