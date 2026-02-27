using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    public interface ISalaryService
    {
        // -- Preview (no DB insert) --
        Task<List<SalaryRecord>> PreviewMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);

        // -- Generation (writes to DB) --
        Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);

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
