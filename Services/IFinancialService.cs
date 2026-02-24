using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cafe.Models.ViewModels;

namespace Cafe.Services
{
    public interface IFinancialService
    {
        Task<FinancialDashboardViewModel> GetDashboardAsync(int year, int month, int? branchId);
        Task<List<MonthlyTrendItem>> GetMonthlyTrendsAsync(int year, int? branchId);
    }
}
