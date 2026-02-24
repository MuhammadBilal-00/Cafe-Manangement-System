using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class FinancialService : IFinancialService
    {
        private readonly ApplicationDbContext _context;

        public FinancialService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<FinancialDashboardViewModel> GetDashboardAsync(int year, int month, int? branchId)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();

            // Revenue from completed orders
            var ordersQuery = _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate < endDate && o.Status == "Completed");

            // Salary expense from salary records
            var salaryQuery = _context.SalaryRecords
                .Where(sr => sr.Year == year && sr.Month == month);

            // Expenses
            var expenseQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate < endDate && e.ApprovalStatus == "Approved");

            if (branchId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.BranchId == branchId.Value);
                salaryQuery = salaryQuery.Where(sr => sr.BranchId == branchId.Value);
                expenseQuery = expenseQuery.Where(e => e.BranchId == branchId.Value);
            }

            var totalRevenue = await ordersQuery.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            var totalSalaryExpense = await salaryQuery.SumAsync(sr => (decimal?)sr.FinalSalary) ?? 0;
            var totalOtherExpenses = await expenseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0;

            // Branch-level summaries
            var branchSummaries = new List<BranchFinancialSummary>();
            var targetBranches = branchId.HasValue
                ? branches.Where(b => b.Id == branchId.Value).ToList()
                : branches;

            foreach (var branch in targetBranches)
            {
                var branchRevenue = await _context.Orders
                    .Where(o => o.BranchId == branch.Id && o.OrderDate >= startDate && o.OrderDate < endDate && o.Status == "Completed")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

                var branchSalary = await _context.SalaryRecords
                    .Where(sr => sr.BranchId == branch.Id && sr.Year == year && sr.Month == month)
                    .SumAsync(sr => (decimal?)sr.FinalSalary) ?? 0;

                var branchExpenses = await _context.Expenses
                    .Where(e => e.BranchId == branch.Id && e.ExpenseDate >= startDate && e.ExpenseDate < endDate && e.ApprovalStatus == "Approved")
                    .SumAsync(e => (decimal?)e.Amount) ?? 0;

                var branchOrders = await _context.Orders
                    .CountAsync(o => o.BranchId == branch.Id && o.OrderDate >= startDate && o.OrderDate < endDate && o.Status == "Completed");

                branchSummaries.Add(new BranchFinancialSummary
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    Revenue = branchRevenue,
                    SalaryExpense = branchSalary,
                    OtherExpenses = branchExpenses,
                    NetProfit = branchRevenue - branchSalary - branchExpenses,
                    TotalOrders = branchOrders
                });
            }

            // Expense breakdown by category
            var expenseCategories = await _context.Expenses
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate < endDate && e.ApprovalStatus == "Approved")
                .Where(e => !branchId.HasValue || e.BranchId == branchId.Value)
                .GroupBy(e => e.Category)
                .Select(g => new ExpenseCategorySummary
                {
                    Category = g.Key,
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Amount)
                .ToListAsync();

            return new FinancialDashboardViewModel
            {
                Branches = branches,
                BranchId = branchId,
                Year = year,
                Month = month,
                TotalRevenue = totalRevenue,
                TotalSalaryExpense = totalSalaryExpense,
                TotalOtherExpenses = totalOtherExpenses,
                NetProfit = totalRevenue - totalSalaryExpense - totalOtherExpenses,
                BranchSummaries = branchSummaries,
                ExpensesByCategory = expenseCategories,
                MonthlyTrends = await GetMonthlyTrendsAsync(year, branchId)
            };
        }

        public async Task<List<MonthlyTrendItem>> GetMonthlyTrendsAsync(int year, int? branchId)
        {
            var trends = new List<MonthlyTrendItem>();

            for (int m = 1; m <= 12; m++)
            {
                var start = new DateTime(year, m, 1);
                var end = start.AddMonths(1);

                if (start > DateTime.Now) break;

                var revenueQuery = _context.Orders
                    .Where(o => o.OrderDate >= start && o.OrderDate < end && o.Status == "Completed");
                var salaryQuery = _context.SalaryRecords
                    .Where(sr => sr.Year == year && sr.Month == m);
                var expenseQuery = _context.Expenses
                    .Where(e => e.ExpenseDate >= start && e.ExpenseDate < end && e.ApprovalStatus == "Approved");

                if (branchId.HasValue)
                {
                    revenueQuery = revenueQuery.Where(o => o.BranchId == branchId.Value);
                    salaryQuery = salaryQuery.Where(sr => sr.BranchId == branchId.Value);
                    expenseQuery = expenseQuery.Where(e => e.BranchId == branchId.Value);
                }

                var rev = await revenueQuery.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
                var sal = await salaryQuery.SumAsync(sr => (decimal?)sr.FinalSalary) ?? 0;
                var exp = await expenseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0;

                trends.Add(new MonthlyTrendItem
                {
                    Year = year,
                    Month = m,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                    Revenue = rev,
                    TotalExpenses = sal + exp,
                    Profit = rev - sal - exp
                });
            }

            return trends;
        }
    }
}
