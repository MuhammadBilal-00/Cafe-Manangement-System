namespace Cafe.Models.ViewModels
{
    public class InventoryDashboardViewModel
    {
        public int TotalItems { get; set; }
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
        public int InStockItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public List<InventoryItemViewModel> RecentlyUpdated { get; set; } = new List<InventoryItemViewModel>();
        public List<InventoryItemViewModel> LowStockAlerts { get; set; } = new List<InventoryItemViewModel>();
    }

    public class InventoryItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal MinimumThreshold { get; set; }
        public decimal CostPerUnit { get; set; }
        public string? Supplier { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Status { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal TotalValue => CurrentQuantity * CostPerUnit;
    }

    public class StockInViewModel
    {
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }

    public class StockOutViewModel
    {
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string TransactionType { get; set; } = "Stock Out"; // Stock Out, Wastage, Expiry
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }

    public class InventoryTransactionViewModel
    {
        public int Id { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? PerformedBy { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }

    public class RecipeMappingViewModel
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public decimal QuantityRequired { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
