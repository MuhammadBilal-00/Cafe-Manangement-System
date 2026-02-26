using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    public interface ISalaryService
    {
        //  Constants 
        const decimal OvertimeMultiplier = 1.5m;
        const decimal AttendanceBonusPercentage = 5m;   // 5% of BaseSalary
        const int MaxLateForBonus = 2;                   // <= 2 late days still qualifies

        //  Salary Generation 
        Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);

        //  Lookups 
        Task<SalaryRecord?> GetSalaryRecordAsync(int id);
        Task<bool> HasSalariesGeneratedAsync(int year, int month, int? branchId);
        Task<decimal> CalculateBaseSalaryForStaff(int staffId);

        //  Workflow 
        Task<bool> FinalizeSalaryAsync(int id, int userId);
        Task<bool> UnlockSalaryAsync(int id, int userId);

        //  Payment 
        Task<bool> MarkAsPaidAsync(int id);

        //  Adjustments 
        Task<SalaryAdjustment> AddAdjustmentAsync(int salaryRecordId, string type, decimal amount, string? reason, int? createdById);
        Task<bool> RemoveAdjustmentAsync(int adjustmentId);
        Task RecalculateSalaryAsync(int salaryRecordId);
    }
}
