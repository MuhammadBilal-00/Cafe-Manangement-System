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
        private readonly ISalaryCalculationService _calcService;

        public SalaryService(ApplicationDbContext context, ISalaryCalculationService calcService)
        {
            _context = context;
            _calcService = calcService;
        }

        // ================================================================
        //  PREVIEW (read-only, no DB writes)
        // ================================================================

        public async Task<List<SalaryRecord>> PreviewMonthlySalariesAsync(
            int year, int month, int? branchId, int? generatedById)
        {
            var staffList = await GetActiveStaffAsync(branchId);
            var previews = new List<SalaryRecord>();

            foreach (var staff in staffList)
            {
                // Skip if already generated
                var existing = await _context.SalaryRecords
                    .FirstOrDefaultAsync(sr => sr.StaffId == staff.Id && sr.Year == year && sr.Month == month);
                if (existing != null)
                {
                    previews.Add(existing);
                    continue;
                }

                var preview = await _calcService.CalculateSalaryAsync(staff, year, month, generatedById);
                previews.Add(preview);
            }

            return previews;
        }

        // ================================================================
        //  GENERATE (writes to DB inside transaction)
        // ================================================================

        public async Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(
            int year, int month, int? branchId, int? generatedById)
        {
            var staffList = await GetActiveStaffAsync(branchId);
            var results = new List<SalaryRecord>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var staff in staffList)
                {
                    // Skip if already generated (prevent duplicate)
                    var existing = await _context.SalaryRecords
                        .FirstOrDefaultAsync(sr => sr.StaffId == staff.Id && sr.Year == year && sr.Month == month);
                    if (existing != null)
                    {
                        results.Add(existing);
                        continue;
                    }

                    var record = await _calcService.CalculateSalaryAsync(staff, year, month, generatedById);
                    _context.SalaryRecords.Add(record);
                    results.Add(record);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return results;
        }

        // ================================================================
        //  RECALCULATION (after adjustments, Draft only)
        // ================================================================

        public async Task RecalculateSalaryAsync(int salaryRecordId)
        {
            var record = await _context.SalaryRecords
                .Include(sr => sr.Adjustments)
                .FirstOrDefaultAsync(sr => sr.Id == salaryRecordId);

            if (record == null || record.Status != "Draft") return;

            // Sum manual adjustments
            decimal manualBonus = record.Adjustments
                .Where(a => a.Type == "Bonus").Sum(a => a.Amount);
            decimal manualDeduction = record.Adjustments
                .Where(a => a.Type == "Deduction").Sum(a => a.Amount);

            // GrossSalary = BaseSalary + OvertimePay + AttendanceBonus + ManualBonuses
            record.GrossSalary = record.BaseSalary + record.OvertimePay +
                                 record.AttendanceBonus + manualBonus;

            // TotalDeductions = formula deductions + manual deductions
            record.TotalDeductions = record.AbsenceDeduction + record.HalfDayDeduction +
                                     record.LatePenaltyDeduction + manualDeduction;

            // Legacy aggregate columns
            record.BonusAmount = record.AttendanceBonus + record.OvertimePay + manualBonus;
            record.DeductionAmount = record.TotalDeductions;

            // Build reason strings
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

        // ================================================================
        //  LOOKUPS
        // ================================================================

        public async Task<SalaryRecord?> GetSalaryRecordAsync(int id)
        {
            return await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Include(sr => sr.GeneratedBy)
                .Include(sr => sr.FinalizedBy)
                .Include(sr => sr.PolicyUsed)
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

        // ================================================================
        //  WORKFLOW
        // ================================================================

        public async Task<bool> FinalizeSalaryAsync(int id, int userId)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.Status != "Draft") return false;

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
            // Cannot unlock Paid
            if (record.PaymentStatus == "Paid") return false;

            record.Status = "Draft";
            record.UnlockedById = userId;
            record.UnlockedAt = DateTime.Now;
            record.FinalizedById = null;
            record.FinalizedAt = null;

            await _context.SaveChangesAsync();
            return true;
        }

        // ================================================================
        //  PAYMENT
        // ================================================================

        public async Task<bool> MarkAsPaidAsync(int id)
        {
            var record = await _context.SalaryRecords.FindAsync(id);
            if (record == null || record.PaymentStatus == "Paid") return false;
            if (record.Status != "Finalized") return false;

            record.Status = "Paid";
            record.PaymentStatus = "Paid";
            record.PaidDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        // ================================================================
        //  ADJUSTMENTS
        // ================================================================

        public async Task<SalaryAdjustment> AddAdjustmentAsync(
            int salaryRecordId, string type, decimal amount, string? reason, int? createdById)
        {
            var record = await _context.SalaryRecords.FindAsync(salaryRecordId);
            if (record == null)
                throw new InvalidOperationException("Salary record not found.");
            if (record.Status != "Draft")
                throw new InvalidOperationException("Adjustments are only allowed on Draft salary records.");

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

            await RecalculateSalaryAsync(salaryRecordId);
            return adjustment;
        }

        public async Task<bool> RemoveAdjustmentAsync(int adjustmentId)
        {
            var adj = await _context.SalaryAdjustments.FindAsync(adjustmentId);
            if (adj == null) return false;

            var record = await _context.SalaryRecords.FindAsync(adj.SalaryRecordId);
            if (record != null && record.Status != "Draft") return false;

            int salaryRecordId = adj.SalaryRecordId;
            _context.SalaryAdjustments.Remove(adj);
            await _context.SaveChangesAsync();

            await RecalculateSalaryAsync(salaryRecordId);
            return true;
        }

        // ================================================================
        //  STAFF BASE SALARY MANAGEMENT (History-aware)
        // ================================================================

        public async Task UpdateBaseSalaryAsync(int staffId, decimal newBaseSalary, int changedById, string? reason)
        {
            // Close current active salary record
            var current = await _context.StaffSalaries
                .Where(ss => ss.StaffId == staffId && ss.IsActive)
                .OrderByDescending(ss => ss.EffectiveFromDate)
                .FirstOrDefaultAsync();

            if (current != null)
            {
                current.IsActive = false;
                current.EffectiveToDate = DateTime.Now.Date.AddDays(-1);
            }

            // Insert new record
            var newRecord = new StaffSalary
            {
                StaffId = staffId,
                BaseSalary = newBaseSalary,
                HourlyRate = newBaseSalary / (26m * 8m), // ~26 working days, 8h each
                PaymentType = current?.PaymentType ?? "Monthly",
                EffectiveFromDate = DateTime.Now.Date,
                EffectiveToDate = null,
                IsActive = true,
                CreatedBy = changedById,
                ChangeReason = reason,
                CreatedDate = DateTime.Now
            };

            _context.StaffSalaries.Add(newRecord);
            await _context.SaveChangesAsync();
        }

        public async Task<List<StaffSalary>> GetBaseSalaryHistoryAsync(int staffId)
        {
            return await _context.StaffSalaries
                .Include(ss => ss.CreatedByUser)
                .Where(ss => ss.StaffId == staffId)
                .OrderByDescending(ss => ss.EffectiveFromDate)
                .ToListAsync();
        }

        // ================================================================
        //  HELPERS
        // ================================================================

        private async Task<List<Staff>> GetActiveStaffAsync(int? branchId)
        {
            var query = _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .Where(s => s.IsActive);

            if (branchId.HasValue)
                query = query.Where(s => s.BranchId == branchId.Value);

            return await query.ToListAsync();
        }
    }
}
