using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    /// <summary>
    /// Outcome of a payroll run. Staff without a configured base salary are never paid an
    /// invented amount — they are skipped and reported by name so HR can fix the setup.
    /// </summary>
    public record SalaryRunResult(List<SalaryRecord> Records, List<string> SkippedStaff);

    public interface ISalaryService
    {
        // -- Preview (no DB insert) --
        Task<SalaryRunResult> PreviewMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);

        // -- Generation (writes to DB) --
        Task<SalaryRunResult> GenerateMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);

        // -- Lookups --
        Task<SalaryRecord?> GetSalaryRecordAsync(int id);
        Task<bool> HasSalariesGeneratedAsync(int year, int month, int? branchId);

        // -- Workflow --
        Task<bool> FinalizeSalaryAsync(int id, int userId);
        Task<bool> UnlockSalaryAsync(int id, int userId);

        // -- Payment --
        Task<bool> MarkAsPaidAsync(int id);

        // -- Adjustments --
        Task<SalaryAdjustment> AddAdjustmentAsync(int salaryRecordId, string type, decimal amount, string? reason, int? createdById);
        Task<bool> RemoveAdjustmentAsync(int adjustmentId);
        Task RecalculateSalaryAsync(int salaryRecordId);

        // -- Staff Base Salary Management --
        Task UpdateBaseSalaryAsync(int staffId, decimal newBaseSalary, int changedById, string? reason);
        Task<List<StaffSalary>> GetBaseSalaryHistoryAsync(int staffId);
    }
}
