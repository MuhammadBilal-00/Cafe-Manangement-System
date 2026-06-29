using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class FinancialDashboardViewModel
    {
        public List<Branch> Branches { get; set; } = new();
        public int? BranchId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public int Month { get; set; } = DateTime.Now.Month;

        // Summary Cards
        public decimal TotalRevenue { get; set; }
        public decimal TotalCostOfGoodsSold { get; set; }
        public decimal TotalSalaryExpense { get; set; }
        public decimal TotalOtherExpenses { get; set; }
        public decimal NetProfit { get; set; }

        // Breakdown lists
        public List<BranchFinancialSummary> BranchSummaries { get; set; } = new();
        public List<ExpenseCategorySummary> ExpensesByCategory { get; set; } = new();
        public List<MonthlyTrendItem> MonthlyTrends { get; set; } = new();
    }

    public class BranchFinancialSummary
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal SalaryExpense { get; set; }
        public decimal OtherExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public int TotalOrders { get; set; }
    }

    public class ExpenseCategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyTrendItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal Profit { get; set; }
    }

    public class ExpenseIndexViewModel
    {
        public List<Expense> Expenses { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();

        // Filters
        public int? BranchId { get; set; }
        public string? Category { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; } = 25;

        // Summary
        public decimal TotalAmount { get; set; }
    }
}
