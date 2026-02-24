using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models;

namespace Cafe.Services
{
    public interface ISalaryService
    {
        Task<List<SalaryRecord>> GenerateMonthlySalariesAsync(int year, int month, int? branchId, int? generatedById);
        Task<SalaryRecord?> GetSalaryRecordAsync(int id);
        Task<bool> MarkAsPaidAsync(int id);
        Task<bool> HasSalariesGeneratedAsync(int year, int month, int? branchId);
        Task<decimal> CalculateBaseSalaryForStaff(int staffId);
    }
}
