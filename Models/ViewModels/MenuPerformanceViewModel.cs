using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class MenuPerformanceViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = "All Branches";
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int TopN { get; set; } = 10;

        // Summary KPIs
        public int TotalItemsSold { get; set; }
        public decimal TotalMenuRevenue { get; set; }
        public int UniqueItemsOrdered { get; set; }
        public decimal AverageItemPrice { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal OverallProfitMargin { get; set; }

        // Detail rows (all items, ordered by revenue desc)
        public List<MenuItemPerformanceRow> Items { get; set; } = new();
        public List<CategoryPerformanceRow> Categories { get; set; } = new();

        // Convenience slices for UI
        public List<MenuItemPerformanceRow> TopSellers { get; set; } = new();
        public List<MenuItemPerformanceRow> LeastSellers { get; set; } = new();

        // Available filter options
        public List<Branch> AvailableBranches { get; set; } = new();
        public List<Category> AvailableCategories { get; set; } = new();
    }

    public class MenuItemPerformanceRow
    {
        public int MenuItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryColor { get; set; }
        public string? BranchName { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit => Revenue - (CostPrice * QuantitySold);
        public decimal ProfitMargin => Revenue > 0 ? Profit / Revenue * 100 : 0;
        public int OrderCount { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class CategoryPerformanceRow
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int ItemCount { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public decimal AvgItemPrice { get; set; }
        public decimal RevenueSharePct { get; set; }
    }
}
