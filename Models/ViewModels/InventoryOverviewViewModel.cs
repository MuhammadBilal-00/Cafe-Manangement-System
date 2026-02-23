using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class InventoryOverviewViewModel
    {
        public int? SelectedBranchId { get; set; }
        public List<InventoryBranchSummaryRow> Branches { get; set; } = new();
        public int TotalItems { get; set; }
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
    }

    public class InventoryBranchSummaryRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? Location { get; set; }

        public int ItemCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public decimal TotalValue { get; set; }

        public decimal LowStockRate => ItemCount == 0 ? 0 : (decimal)LowStockCount / ItemCount * 100m;
        public decimal OutOfStockRate => ItemCount == 0 ? 0 : (decimal)OutOfStockCount / ItemCount * 100m;
    }
}