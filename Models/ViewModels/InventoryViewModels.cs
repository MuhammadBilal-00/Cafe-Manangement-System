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
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime LastUpdated { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public decimal TotalValue => Quantity * UnitPrice;
        
        // Computed properties for UI
        public string Status
        {
            get
            {
                if (Quantity == 0) return "Out of Stock";
                if (Quantity <= ReorderLevel) return "Low Stock";
                return "In Stock";
            }
        }
    }

    public class StockInViewModel
    {
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; }
        public decimal Quantity { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }

    public class StockOutViewModel
    {
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; }
        public decimal Quantity { get; set; }
        public string TransactionType { get; set; } = "Stock Out"; // Stock Out, Wastage, Expiry
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }

    public class InventoryTransactionViewModel
    {
        public int Id { get; set; }
        public string InventoryItemName { get; set; }
        public string TransactionType { get; set; }
        public decimal Quantity { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? PerformedBy { get; set; }
        public string BranchName { get; set; }
    }

    public class RecipeMappingViewModel
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }
        public int InventoryItemId { get; set; }
        public string InventoryItemName { get; set; }
        public decimal QuantityRequired { get; set; }
        public string Unit { get; set; }
    }
}
