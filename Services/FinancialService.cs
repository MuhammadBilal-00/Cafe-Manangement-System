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
    /// <summary>
    /// Operational P&amp;L for the financial dashboard. Revenue is recognized from INVOICES
    /// (net of discounts, before tax) — the financial document created the moment a sale is
    /// finalized — not from the order's kitchen status. A paid ticket still being cooked is
    /// revenue; a cancelled order (whose invoice is voided) never is. This keeps the dashboard
    /// consistent with Receivables and the accounting auto-post, which read the same documents.
    /// </summary>
    public class FinancialService : IFinancialService
    {
        private readonly ApplicationDbContext _context;

        public FinancialService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Invoices that count as sales: everything not voided/declined.</summary>
        private IQueryable<Invoice> RecognizedInvoices() =>
            _context.Invoices.Where(i => i.PaymentStatus != "Cancelled" && i.PaymentStatus != "Failed");

        public async Task<FinancialDashboardViewModel> GetDashboardAsync(int year, int month, int? branchId)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();

            // ── Whole-month aggregates, one grouped query per source ──
            var revenueByBranch = await RecognizedInvoices()
                .Where(i => i.CreatedAt >= startDate && i.CreatedAt < endDate)
                .GroupBy(i => i.BranchId)
                .Select(g => new
                {
                    BranchId = g.Key,
                    Net = g.Sum(i => i.Subtotal - i.PromoDiscount - i.PartnershipDiscount),
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.BranchId, x => new { x.Net, x.Count });

            var cogsByBranch = await CogsByBranchAsync(startDate, endDate);

            var salaryByBranch = await _context.SalaryRecords
                .Where(sr => sr.Year == year && sr.Month == month)
                .GroupBy(sr => sr.BranchId)
                .Select(g => new { BranchId = g.Key, V = g.Sum(x => x.FinalSalary) })
                .ToDictionaryAsync(x => x.BranchId, x => x.V);

            var expenseByBranch = await _context.Expenses
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate < endDate && e.ApprovalStatus == "Approved")
                .GroupBy(e => e.BranchId)
                .Select(g => new { BranchId = g.Key, V = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.BranchId, x => x.V);

            var targetBranches = branchId.HasValue
                ? branches.Where(b => b.Id == branchId.Value).ToList()
                : branches;

            var branchSummaries = targetBranches.Select(branch =>
            {
                var rev = revenueByBranch.TryGetValue(branch.Id, out var r) ? r : null;
                var cogs = cogsByBranch.GetValueOrDefault(branch.Id);
                var sal = salaryByBranch.GetValueOrDefault(branch.Id);
                var exp = expenseByBranch.GetValueOrDefault(branch.Id);
                return new BranchFinancialSummary
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    Revenue = rev?.Net ?? 0,
                    CostOfGoodsSold = cogs,
                    SalaryExpense = sal,
                    OtherExpenses = exp,
                    NetProfit = (rev?.Net ?? 0) - cogs - sal - exp,
                    TotalOrders = rev?.Count ?? 0
                };
            }).ToList();

            var totalRevenue = branchSummaries.Sum(b => b.Revenue);
            var totalCogs = branchSummaries.Sum(b => b.CostOfGoodsSold);
            var totalSalaryExpense = branchSummaries.Sum(b => b.SalaryExpense);
            var totalOtherExpenses = branchSummaries.Sum(b => b.OtherExpenses);

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
                TotalCostOfGoodsSold = totalCogs,
                TotalSalaryExpense = totalSalaryExpense,
                TotalOtherExpenses = totalOtherExpenses,
                NetProfit = totalRevenue - totalCogs - totalSalaryExpense - totalOtherExpenses,
                BranchSummaries = branchSummaries,
                ExpensesByCategory = expenseCategories,
                MonthlyTrends = await GetMonthlyTrendsAsync(year, branchId)
            };
        }

        // Cost of goods sold per branch: Σ(quantity × MenuItem.CostPrice) over the order lines
        // of invoiced (recognized) sales in the range.
        private async Task<Dictionary<int, decimal>> CogsByBranchAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.OrderItems
                .Where(oi => _context.Invoices.Any(i => i.OrderId == oi.OrderId
                    && i.PaymentStatus != "Cancelled" && i.PaymentStatus != "Failed"
                    && i.CreatedAt >= startDate && i.CreatedAt < endDate))
                .GroupBy(oi => oi.Order.BranchId)
                .Select(g => new { BranchId = g.Key, V = g.Sum(oi => oi.Quantity * oi.MenuItem.CostPrice) })
                .ToDictionaryAsync(x => x.BranchId, x => x.V);
        }

        public async Task<List<MonthlyTrendItem>> GetMonthlyTrendsAsync(int year, int? branchId)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = yearStart.AddYears(1);

            // One grouped query per source for the whole year (previously 4 queries × 12 months).
            var revenueByMonth = await RecognizedInvoices()
                .Where(i => i.CreatedAt >= yearStart && i.CreatedAt < yearEnd)
                .Where(i => !branchId.HasValue || i.BranchId == branchId.Value)
                .GroupBy(i => i.CreatedAt.Month)
                .Select(g => new { Month = g.Key, V = g.Sum(i => i.Subtotal - i.PromoDiscount - i.PartnershipDiscount) })
                .ToDictionaryAsync(x => x.Month, x => x.V);

            var cogsByMonth = await _context.OrderItems
                .Where(oi => _context.Invoices.Any(i => i.OrderId == oi.OrderId
                    && i.PaymentStatus != "Cancelled" && i.PaymentStatus != "Failed"
                    && i.CreatedAt >= yearStart && i.CreatedAt < yearEnd
                    && (!branchId.HasValue || i.BranchId == branchId.Value)))
                .GroupBy(oi => oi.Order.OrderDate.Month)
                .Select(g => new { Month = g.Key, V = g.Sum(oi => oi.Quantity * oi.MenuItem.CostPrice) })
                .ToDictionaryAsync(x => x.Month, x => x.V);

            var salaryByMonth = await _context.SalaryRecords
                .Where(sr => sr.Year == year)
                .Where(sr => !branchId.HasValue || sr.BranchId == branchId.Value)
                .GroupBy(sr => sr.Month)
                .Select(g => new { Month = g.Key, V = g.Sum(x => x.FinalSalary) })
                .ToDictionaryAsync(x => x.Month, x => x.V);

            var expenseByMonth = await _context.Expenses
                .Where(e => e.ExpenseDate >= yearStart && e.ExpenseDate < yearEnd && e.ApprovalStatus == "Approved")
                .Where(e => !branchId.HasValue || e.BranchId == branchId.Value)
                .GroupBy(e => e.ExpenseDate.Month)
                .Select(g => new { Month = g.Key, V = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.Month, x => x.V);

            var trends = new List<MonthlyTrendItem>();
            for (int m = 1; m <= 12; m++)
            {
                if (new DateTime(year, m, 1) > DateTime.Now) break;

                var rev = revenueByMonth.GetValueOrDefault(m);
                var cogs = cogsByMonth.GetValueOrDefault(m);
                var sal = salaryByMonth.GetValueOrDefault(m);
                var exp = expenseByMonth.GetValueOrDefault(m);

                trends.Add(new MonthlyTrendItem
                {
                    Year = year,
                    Month = m,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m),
                    Revenue = rev,
                    CostOfGoodsSold = cogs,
                    TotalExpenses = cogs + sal + exp,
                    Profit = rev - cogs - sal - exp
                });
            }

            return trends;
        }
    }
}
