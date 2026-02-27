using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    /// <summary>
    /// Pure salary calculation engine. No DB writes — only computes a breakdown
    /// from attendance stats, base salary, and the active salary policy.
    /// Used for both Preview (no DB) and Generate (writes via SalaryService).
    /// </summary>
    public interface ISalaryCalculationService
    {
        /// <summary>
        /// Calculate a full salary breakdown for a single staff member.
        /// Does NOT write to DB — returns a detached SalaryRecord.
        /// </summary>
        Task<SalaryRecord> CalculateSalaryAsync(Staff staff, int year, int month, int? generatedById);

        /// <summary>
        /// Calculate from an explicit policy + baseSalary + stats (for preview without DB reads).
        /// </summary>
        SalaryRecord CalculateSalary(Staff staff, decimal baseSalary, AttendanceStats stats,
            SalaryPolicy policy, int year, int month, int? generatedById);

        /// <summary>
        /// Fetch the effective base salary for a staff member on a given month.
        /// Finds the StaffSalary record whose EffectiveFrom ≤ month-end and (EffectiveTo is null or ≥ month-start).
        /// </summary>
        Task<decimal> GetEffectiveBaseSalaryAsync(int staffId, int year, int month);

        /// <summary>
        /// Fetch the salary policy that was active on the 1st of the given month.
        /// </summary>
        Task<SalaryPolicy?> GetEffectivePolicyAsync(int year, int month);
    }
}
