using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface IInventoryService
    {
        Task<bool> StockIn(int inventoryItemId, decimal quantity, string? notes, string performedBy);
        Task<bool> StockOut(int inventoryItemId, decimal quantity, string transactionType, string? notes, string performedBy);
        Task<bool> DeductInventoryForOrder(int orderId, int branchId, string performedBy);
        Task<bool> CheckInventoryAvailability(int menuItemId, int quantity, int branchId);
        Task UpdateInventoryStatus(int inventoryItemId);
        Task<string> GetInventoryStatus(decimal currentQuantity, decimal minimumThreshold);
    }

    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> StockIn(int inventoryItemId, decimal quantity, string? notes, string performedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.InventoryItems.FindAsync(inventoryItemId);
                if (item == null || quantity <= 0)
                    return false;

                var quantityBefore = item.CurrentQuantity;
                item.CurrentQuantity += quantity;
                item.LastUpdated = DateTime.Now;

                // Create transaction record
                var inventoryTransaction = new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = "Stock In",
                    Quantity = quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = item.CurrentQuantity,
                    Notes = notes,
                    BranchId = item.BranchId,
                    PerformedBy = performedBy,
                    TransactionDate = DateTime.Now
                };

                _context.InventoryTransactions.Add(inventoryTransaction);
                await UpdateInventoryStatus(inventoryItemId);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> StockOut(int inventoryItemId, decimal quantity, string transactionType, string? notes, string performedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.InventoryItems.FindAsync(inventoryItemId);
                if (item == null || quantity <= 0 || item.CurrentQuantity < quantity)
                    return false;

                var quantityBefore = item.CurrentQuantity;
                item.CurrentQuantity -= quantity;
                item.LastUpdated = DateTime.Now;

                // Create transaction record
                var inventoryTransaction = new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = transactionType,
                    Quantity = quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = item.CurrentQuantity,
                    Notes = notes,
                    BranchId = item.BranchId,
                    PerformedBy = performedBy,
                    TransactionDate = DateTime.Now
                };

                _context.InventoryTransactions.Add(inventoryTransaction);
                await UpdateInventoryStatus(inventoryItemId);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> DeductInventoryForOrder(int orderId, int branchId, string performedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                    return false;

                foreach (var orderItem in order.OrderItems)
                {
                    // Get recipe mappings for this menu item
                    var recipeMappings = await _context.InventoryRecipeMappings
                        .Include(rm => rm.InventoryItem)
                        .Where(rm => rm.MenuItemId == orderItem.MenuItemId 
                                  && rm.InventoryItem.BranchId == branchId)
                        .ToListAsync();

                    foreach (var mapping in recipeMappings)
                    {
                        var requiredQuantity = mapping.QuantityRequired * orderItem.Quantity;
                        var item = mapping.InventoryItem;

                        if (item.CurrentQuantity < requiredQuantity)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }

                        var quantityBefore = item.CurrentQuantity;
                        item.CurrentQuantity -= requiredQuantity;
                        item.LastUpdated = DateTime.Now;

                        // Create transaction record
                        var inventoryTransaction = new InventoryTransaction
                        {
                            InventoryItemId = item.Id,
                            TransactionType = "Order Usage",
                            Quantity = requiredQuantity,
                            QuantityBefore = quantityBefore,
                            QuantityAfter = item.CurrentQuantity,
                            Notes = $"Used for Order #{order.OrderNumber}",
                            BranchId = branchId,
                            OrderId = orderId,
                            PerformedBy = performedBy,
                            TransactionDate = DateTime.Now
                        };

                        _context.InventoryTransactions.Add(inventoryTransaction);
                        await UpdateInventoryStatus(item.Id);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> CheckInventoryAvailability(int menuItemId, int quantity, int branchId)
        {
            var recipeMappings = await _context.InventoryRecipeMappings
                .Include(rm => rm.InventoryItem)
                .Where(rm => rm.MenuItemId == menuItemId && rm.InventoryItem.BranchId == branchId)
                .ToListAsync();

            foreach (var mapping in recipeMappings)
            {
                var requiredQuantity = mapping.QuantityRequired * quantity;
                if (mapping.InventoryItem.CurrentQuantity < requiredQuantity)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task UpdateInventoryStatus(int inventoryItemId)
        {
            var item = await _context.InventoryItems.FindAsync(inventoryItemId);
            if (item == null)
                return;

            item.Status = await GetInventoryStatus(item.CurrentQuantity, item.MinimumThreshold);
        }

        public async Task<string> GetInventoryStatus(decimal currentQuantity, decimal minimumThreshold)
        {
            if (currentQuantity <= 0)
                return "Out of Stock";
            else if (currentQuantity <= minimumThreshold)
                return "Low Stock";
            else
                return "In Stock";
        }
    }
}
