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

                // Quantity is int in the model; keep a decimal copy for the transaction log
                var quantityBefore = (decimal)item.Quantity;

                // Add decimal quantity to int quantity with rounding
                int add = (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
                item.Quantity += add;
                item.LastUpdated = DateTime.Now;

                var quantityAfter = (decimal)item.Quantity;

                // Create transaction record
                var inventoryTransaction = new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = "Stock In",
                    Quantity = quantity,            // decimal field on transaction
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
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
                if (item == null || quantity <= 0)
                    return false;

                // Convert decimal quantity to int for comparison and updating
                int qtyToRemove = (int)Math.Round(quantity, MidpointRounding.AwayFromZero);

                if (item.Quantity < qtyToRemove)
                    return false;

                var quantityBefore = (decimal)item.Quantity;

                item.Quantity -= qtyToRemove;
                item.LastUpdated = DateTime.Now;

                var quantityAfter = (decimal)item.Quantity;

                // Create transaction record
                var inventoryTransaction = new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = transactionType,
                    Quantity = quantity,              // decimal on transaction
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
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
                    var recipeMappings = await _context.InventoryRecipeMappings
                        .Include(rm => rm.InventoryItem)
                        .Where(rm => rm.MenuItemId == orderItem.MenuItemId
                                  && rm.InventoryItem.BranchId == branchId)
                        .ToListAsync();

                    foreach (var mapping in recipeMappings)
                    {
                        // QuantityRequired is probably decimal; multiply by int quantity
                        var requiredQuantityDecimal = mapping.QuantityRequired * orderItem.Quantity;
                        int requiredQuantity = (int)Math.Round(requiredQuantityDecimal, MidpointRounding.AwayFromZero);

                        var item = mapping.InventoryItem;

                        if (item.Quantity < requiredQuantity)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }

                        var quantityBefore = (decimal)item.Quantity;

                        item.Quantity -= requiredQuantity;
                        item.LastUpdated = DateTime.Now;

                        var quantityAfter = (decimal)item.Quantity;

                        // Create transaction record
                        var inventoryTransaction = new InventoryTransaction
                        {
                            InventoryItemId = item.Id,
                            TransactionType = "Order Usage",
                            Quantity = requiredQuantityDecimal, // decimal on transaction
                            QuantityBefore = quantityBefore,
                            QuantityAfter = quantityAfter,
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
                // mapping.QuantityRequired likely decimal; multiply by int quantity
                var requiredQuantityDecimal = mapping.QuantityRequired * quantity;
                int requiredQuantity = (int)Math.Round(requiredQuantityDecimal, MidpointRounding.AwayFromZero);

                if (mapping.InventoryItem.Quantity < requiredQuantity)
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

            // Currently no Status field on InventoryItem – nothing to update here now.
            // Left as a hook in case you add a Status column/property later.
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