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

        public SalaryService(ApplicationDbContext context, IAttendanceService attendanceService)
        {
            _context = context;
            _attendanceService = attendanceService;
        }

        // 
        //  SALARY GENERATION (Formula-Based)
        // 

        public async Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(
            int year, int month, int? branchId, int? generatedById)
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
                // Skip if already generated
                var existing = await _context.SalaryRecords
                    .FirstOrDefaultAsync(sr => sr.StaffId == staff.Id && sr.Year == year && sr.Month == month);
                if (existing != null)
                {
                    results.Add(existing);
                    continue;
                }

                var stats = await _attendanceService.GetStaffMonthlyStatsAsync(staff.Id, year, month);
                var baseSalary = await CalculateBaseSalaryForStaff(staff.Id);

                var record = BuildSalaryRecord(staff, baseSalary, stats, year, month, generatedById);

                _context.SalaryRecords.Add(record);
                results.Add(record);
            }

            await _context.SaveChangesAsync();
            return results;
        }

        /// <summary>
        /// Core formula engine. Builds a SalaryRecord from attendance stats.
        /// </summary>
        private SalaryRecord BuildSalaryRecord(Staff staff, decimal baseSalary,
            AttendanceStats stats, int year, int month, int? generatedById)
        {
            int workingDays = stats.TotalWorkingDays;
            if (workingDays <= 0) workingDays = 1; // safety

            //  Daily & Hourly Rates 
            decimal dailyRate = baseSalary / workingDays;
            decimal hourlyRate = dailyRate / IAttendanceService.StandardDailyHours;

            //  Deductions 
            decimal absenceDeduction = stats.DaysAbsent * dailyRate;
            decimal halfDayDeduction = stats.DaysHalfDay * (dailyRate / 2m);

            // Late penalty: every 3 late = 1 half-day deduction
            int latePenaltyDays = stats.DaysLate / 3;
            decimal latePenaltyDeduction = latePenaltyDays * (dailyRate / 2m);

            decimal totalDeductions = absenceDeduction + halfDayDeduction + latePenaltyDeduction;

            //  Overtime 
            decimal overtimePay = stats.TotalOvertimeHours * hourlyRate * ISalaryService.OvertimeMultiplier;

            //  Attendance Bonus 
            decimal attendanceBonus = 0;
            if (stats.DaysAbsent == 0 && stats.DaysLate <= ISalaryService.MaxLateForBonus)
            {
                attendanceBonus = baseSalary * (ISalaryService.AttendanceBonusPercentage / 100m);
            }

            //  Gross & Net 
            decimal grossSalary = baseSalary + overtimePay + attendanceBonus;
            decimal finalSalary = grossSalary - totalDeductions;
            if (finalSalary < 0) finalSalary = 0;

            //  Build Descriptions 
            var bonusReasons = new List<string>();
            if (attendanceBonus > 0)
                bonusReasons.Add($"Attendance bonus ({ISalaryService.AttendanceBonusPercentage}%)");

            var deductionReasons = new List<string>();
            if (stats.DaysAbsent > 0)
                deductionReasons.Add($"{stats.DaysAbsent} absent day(s)");
            if (stats.DaysHalfDay > 0)
                deductionReasons.Add($"{stats.DaysHalfDay} half-day(s)");
            if (latePenaltyDays > 0)
                deductionReasons.Add($"{stats.DaysLate} late(s) = {latePenaltyDays} half-day penalty");

            return new SalaryRecord
            {
                StaffId = staff.Id,
                BranchId = staff.BranchId,
                Year = year,
                Month = month,
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

        // 
        //  RECALCULATION (after adjustments)
        // 

        public async Task RecalculateSalaryAsync(int salaryRecordId)
        {
            var record = await _context.SalaryRecords
                .Include(sr => sr.Adjustments)
                .FirstOrDefaultAsync(sr => sr.Id == salaryRecordId);

            if (record == null || record.Status == "Finalized") return;

            // Sum manual adjustments from SalaryAdjustment table
            decimal manualBonus = record.Adjustments
                .Where(a => a.Type == "Bonus").Sum(a => a.Amount);
            decimal manualDeduction = record.Adjustments
                .Where(a => a.Type == "Deduction").Sum(a => a.Amount);

            // GrossSalary = BaseSalary + OvertimePay + AttendanceBonus + ManualBonus
            record.GrossSalary = record.BaseSalary + record.OvertimePay +
                                 record.AttendanceBonus + manualBonus;

            // TotalDeductions = AbsenceDeduction + HalfDayDeduction + LatePenaltyDeduction + ManualDeduction
            record.TotalDeductions = record.AbsenceDeduction + record.HalfDayDeduction +
                                     record.LatePenaltyDeduction + manualDeduction;

            // Aggregate columns (for backward compat)
            record.BonusAmount = record.AttendanceBonus + record.OvertimePay + manualBonus;
            record.DeductionAmount = record.TotalDeductions;

            // Build reasons
            var bonusReasons = new List<string>();
            if (record.AttendanceBonus > 0) bonusReasons.Add("Attendance bonus");
            if (record.OvertimePay > 0) bonusReasons.Add($"Overtime ({record.OvertimeHours}h)");
            foreach (var adj in record.Adjustments.Where(a => a.Type == "Bonus"))
                bonusReasons.Add(adj.Reason ?? "Manual bonus");
            record.BonusReason = bonusReasons.Any() ? string.Join("; ", bonusReasons) : null;

            var deductionReasons = new List<string>();
            if (record.AbsenceDeduction > 0) deductionReasons.Add($"{record.DaysAbsent} absent");
            if (record.HalfDayDeduction > 0) deductionReasons.Add($"{record.DaysHalfDay} half-day(s)");
            if (record.LatePenaltyDeduction > 0) deductionReasons.Add("Late penalty");
            foreach (var adj in record.Adjustments.Where(a => a.Type == "Deduction"))
                deductionReasons.Add(adj.Reason ?? "Manual deduction");
            record.DeductionReason = deductionReasons.Any() ? string.Join("; ", deductionReasons) : null;

            record.FinalSalary = Math.Max(0, Math.Round(record.GrossSalary - record.TotalDeductions, 2));

            await _context.SaveChangesAsync();
        }

        // 
        //  LOOKUPS
        // 

        public async Task<SalaryRecord?> GetSalaryRecordAsync(int id)
        {
            return await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Include(sr => sr.GeneratedBy)
                .Include(sr => sr.FinalizedBy)
                .Include(sr => sr.Adjustments).ThenInclude(a => a.CreatedBy)
                .FirstOrDefaultAsync(sr => sr.Id == id);
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
            var activeSalary = await _context.StaffSalaries
                .Where(ss => ss.StaffId == staffId && ss.IsActive)
                .OrderByDescending(ss => ss.EffectiveFromDate)
                .FirstOrDefaultAsync();

            if (activeSalary != null && activeSalary.BaseSalary > 0)
                return activeSalary.BaseSalary;

            var staff = await _context.Staff
                .Include(s => s.StaffRole)
                .FirstOrDefaultAsync(s => s.Id == staffId);

            if (staff?.StaffRole != null && staff.StaffRole.DefaultMonthlySalary > 0)
                return staff.StaffRole.DefaultMonthlySalary;

            return 30000m; // fallback
        }

        // 
        //  WORKFLOW
        // 

        public async Task<bool> FinalizeSalaryAsync(int id, int userId)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.Status == "Finalized") return false;

            record.Status = "Finalized";
            record.FinalizedById = userId;
            record.FinalizedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnlockSalaryAsync(int id, int userId)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.Status != "Finalized") return false;

            record.Status = "Draft";
            record.UnlockedById = userId;
            record.UnlockedAt = DateTime.Now;
            // Reset finalization
            record.FinalizedById = null;
            record.FinalizedAt = null;

            await _context.SaveChangesAsync();
            return true;
        }

        // 
        //  PAYMENT
        // 

        public async Task<bool> MarkAsPaidAsync(int id)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.PaymentStatus == "Paid") return false;

            // Must be finalized before payment
            if (record.Status != "Finalized") return false;

            record.PaymentStatus = "Paid";
            record.PaidDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        // 
        //  ADJUSTMENTS (SalaryAdjustment table)
        // 

        public async Task<SalaryAdjustment> AddAdjustmentAsync(
            int salaryRecordId, string type, decimal amount, string? reason, int? createdById)
        {
            var record = await _context.SalaryRecords.FindAsync(salaryRecordId);
            if (record == null)
                throw new InvalidOperationException("Salary record not found.");
            if (record.Status == "Finalized")
                throw new InvalidOperationException("Cannot adjust a finalized salary record.");

            var adjustment = new SalaryAdjustment
            {
                SalaryRecordId = salaryRecordId,
                Type = type,
                Amount = amount,
                Reason = reason,
                CreatedById = createdById,
                CreatedAt = DateTime.Now
            };

            _context.SalaryAdjustments.Add(adjustment);
            await _context.SaveChangesAsync();

            // Recalculate after adding adjustment
            await RecalculateSalaryAsync(salaryRecordId);

            return adjustment;
        }

        public async Task<bool> RemoveAdjustmentAsync(int adjustmentId)
        {
            var adj = await _context.SalaryAdjustments.FindAsync(adjustmentId);
            if (adj == null) return false;

            var record = await _context.SalaryRecords.FindAsync(adj.SalaryRecordId);
            if (record != null && record.Status == "Finalized") return false;

            int salaryRecordId = adj.SalaryRecordId;
            _context.SalaryAdjustments.Remove(adj);
            await _context.SaveChangesAsync();

            await RecalculateSalaryAsync(salaryRecordId);
            return true;
        }
    }
}
