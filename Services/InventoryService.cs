using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface IInventoryService
    {
        Task<bool> StockIn(int inventoryItemId, decimal quantity, string? notes, string performedBy);
        Task<bool> StockOut(int inventoryItemId, decimal quantity, string transactionType, string? notes, string performedBy);
        Task<bool> AdjustStockAsync(int inventoryItemId, decimal newQuantity, string? reason, string performedBy);
        Task<bool> DeductInventoryForOrder(int orderId, int branchId, string performedBy);

        /// <summary>
        /// Reverse an order's "Order Usage" deductions (cancel before cooking started, or
        /// compensation when finalize fails mid-flight). Restocks exactly what the ledger says
        /// was deducted and writes offsetting audit rows. Idempotent: already-reversed orders
        /// are a no-op.
        /// </summary>
        Task<bool> RestockOrderAsync(int orderId, string reason, string performedBy);

        Task<bool> CheckInventoryAvailability(int menuItemId, int quantity, int branchId);
    }

    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(ApplicationDbContext context, INotificationService notifications,
            ILogger<InventoryService> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<bool> StockIn(int inventoryItemId, decimal quantity, string? notes, string performedBy)
        {
            if (quantity <= 0)
                return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                quantity = Math.Round(quantity, 2);

                // Atomic increment at the DB level — see StockOut for why a read-then-write
                // here would lose updates under concurrent stock-ins on the same item.
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE InventoryItems SET Quantity = Quantity + {quantity}, LastUpdated = GETDATE() WHERE Id = {inventoryItemId}");

                if (rows == 0)
                {
                    await transaction.RollbackAsync();
                    return false; // item not found
                }

                var item = await _context.InventoryItems.AsNoTracking().FirstAsync(i => i.Id == inventoryItemId);
                var quantityAfter = item.Quantity;
                var quantityBefore = quantityAfter - quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = "Stock In",
                    Quantity = quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
                    Notes = notes,
                    BranchId = item.BranchId,
                    PerformedBy = performedBy,
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "StockIn failed for item {ItemId}", inventoryItemId);
                return false;
            }
        }

        public async Task<bool> StockOut(int inventoryItemId, decimal quantity, string transactionType, string? notes, string performedBy)
        {
            if (quantity <= 0)
                return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                quantity = Math.Round(quantity, 2);

                // Atomic conditional decrement: the availability check and the deduction
                // happen in one UPDATE statement, evaluated against the row's *current*
                // committed value. Two concurrent StockOut/order calls can no longer both
                // read "5 in stock" in memory, both pass an availability check, and both
                // deduct — only as many callers as there is real stock will get rows=1;
                // the rest correctly fail instead of driving Quantity negative.
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE InventoryItems SET Quantity = Quantity - {quantity}, LastUpdated = GETDATE() WHERE Id = {inventoryItemId} AND Quantity >= {quantity}");

                if (rows == 0)
                {
                    await transaction.RollbackAsync();
                    return false; // not found or insufficient stock
                }

                var item = await _context.InventoryItems.AsNoTracking().FirstAsync(i => i.Id == inventoryItemId);
                var quantityAfter = item.Quantity;
                var quantityBefore = quantityAfter + quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = transactionType,
                    Quantity = quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
                    Notes = notes,
                    BranchId = item.BranchId,
                    PerformedBy = performedBy,
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await NotifyIfCrossedThresholdAsync(item, quantityBefore, quantityAfter);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "StockOut failed for item {ItemId}", inventoryItemId);
                return false;
            }
        }

        public async Task<bool> AdjustStockAsync(int inventoryItemId, decimal newQuantity, string? reason, string performedBy)
        {
            // Manual stock-count correction: sets an absolute value, so unlike
            // StockIn/StockOut there's no lost-update race to guard against — last write
            // wins is the expected behavior when a manager re-counts and overrides.
            // The one thing that must never happen is a negative resulting quantity.
            if (newQuantity < 0)
                return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.InventoryItems.FindAsync(inventoryItemId);
                if (item == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                newQuantity = Math.Round(newQuantity, 2);
                var quantityBefore = item.Quantity;
                item.Quantity = newQuantity;
                item.LastUpdated = DateTime.Now;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryItemId = inventoryItemId,
                    TransactionType = "Manual Adjustment",
                    Quantity = Math.Abs(newQuantity - quantityBefore),
                    QuantityBefore = quantityBefore,
                    QuantityAfter = newQuantity,
                    Notes = reason,
                    BranchId = item.BranchId,
                    PerformedBy = performedBy,
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "AdjustStock failed for item {ItemId}", inventoryItemId);
                return false;
            }
        }

        public async Task<bool> DeductInventoryForOrder(int orderId, int branchId, string performedBy)
        {
            var lowStockHits = new List<(InventoryItem item, decimal before, decimal after)>();

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
                        .Where(rm => rm.MenuItemId == orderItem.MenuItemId
                                  && rm.InventoryItem.BranchId == branchId)
                        .ToListAsync();

                    foreach (var mapping in recipeMappings)
                    {
                        // Recipes consume fractional quantities (e.g. 0.25 kg per serving) —
                        // deduct the exact decimal amount, never a rounded integer.
                        var requiredQuantity = Math.Round(mapping.QuantityRequired * orderItem.Quantity, 2);
                        if (requiredQuantity <= 0) continue;

                        // Atomic conditional decrement (see StockOut) — two orders placed at
                        // the same instant can no longer both pass a stale in-memory stock
                        // check and both succeed when only one has enough stock to cover.
                        int rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE InventoryItems SET Quantity = Quantity - {requiredQuantity}, LastUpdated = GETDATE() WHERE Id = {mapping.InventoryItemId} AND Quantity >= {requiredQuantity}");

                        if (rows == 0)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }

                        var item = await _context.InventoryItems.AsNoTracking()
                            .FirstAsync(i => i.Id == mapping.InventoryItemId);
                        var quantityAfter = item.Quantity;
                        var quantityBefore = quantityAfter + requiredQuantity;

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            InventoryItemId = item.Id,
                            TransactionType = "Order Usage",
                            Quantity = requiredQuantity,
                            QuantityBefore = quantityBefore,
                            QuantityAfter = quantityAfter,
                            Notes = $"Used for Order #{order.OrderNumber}",
                            BranchId = branchId,
                            OrderId = orderId,
                            PerformedBy = performedBy,
                            TransactionDate = DateTime.Now
                        });

                        lowStockHits.Add((item, quantityBefore, quantityAfter));
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Inventory deduction failed for order {OrderId}", orderId);
                return false;
            }

            // After the deduction is committed, raise low-stock alerts for items that just
            // crossed their threshold (the everyday path that empties the pantry is sales).
            foreach (var (item, before, after) in lowStockHits)
                await NotifyIfCrossedThresholdAsync(item, before, after);

            return true;
        }

        public async Task<bool> RestockOrderAsync(int orderId, string reason, string performedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // What the ledger says this order consumed…
                var consumed = await _context.InventoryTransactions
                    .Where(t => t.OrderId == orderId && t.TransactionType == "Order Usage")
                    .GroupBy(t => t.InventoryItemId)
                    .Select(g => new { InventoryItemId = g.Key, Quantity = g.Sum(t => t.Quantity) })
                    .ToListAsync();
                if (consumed.Count == 0) { await transaction.RollbackAsync(); return true; } // nothing was deducted

                // …minus what has already been reversed (keeps this idempotent).
                var reversed = await _context.InventoryTransactions
                    .Where(t => t.OrderId == orderId && t.TransactionType == "Order Restock")
                    .GroupBy(t => t.InventoryItemId)
                    .Select(g => new { InventoryItemId = g.Key, Quantity = g.Sum(t => t.Quantity) })
                    .ToDictionaryAsync(x => x.InventoryItemId, x => x.Quantity);

                var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);

                foreach (var line in consumed)
                {
                    var toRestore = line.Quantity - reversed.GetValueOrDefault(line.InventoryItemId);
                    if (toRestore <= 0) continue;

                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE InventoryItems SET Quantity = Quantity + {toRestore}, LastUpdated = GETDATE() WHERE Id = {line.InventoryItemId}");

                    var item = await _context.InventoryItems.AsNoTracking().FirstAsync(i => i.Id == line.InventoryItemId);
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        InventoryItemId = line.InventoryItemId,
                        TransactionType = "Order Restock",
                        Quantity = toRestore,
                        QuantityBefore = item.Quantity - toRestore,
                        QuantityAfter = item.Quantity,
                        Notes = $"{reason} (Order #{order?.OrderNumber ?? orderId.ToString()})",
                        BranchId = item.BranchId,
                        OrderId = orderId,
                        PerformedBy = performedBy,
                        TransactionDate = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Restock failed for order {OrderId}", orderId);
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
                if (mapping.InventoryItem.Quantity < requiredQuantity)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Raise a branch-scoped alert when a deduction moves an item from above its threshold
        /// to at/below it. Only the *crossing* fires — repeated sales below the threshold stay
        /// quiet, so purchasing gets one actionable ping instead of noise. Never blocks stock work.
        /// </summary>
        private async Task NotifyIfCrossedThresholdAsync(InventoryItem item, decimal before, decimal after)
        {
            try
            {
                if (item.MinimumStock > 0 && after < item.MinimumStock && before >= item.MinimumStock)
                {
                    await _notifications.CreateNotificationAsync(
                        "Critical Stock Level",
                        $"\"{item.Name}\" dropped to {after:0.##} {item.Unit} (minimum: {item.MinimumStock:0.##}).",
                        "Error", NotificationCategory.Inventory,
                        branchId: item.BranchId, createdBy: null,
                        redirectUrl: "/InventoryItem/LowStock", icon: "fas fa-triangle-exclamation");
                }
                else if (after <= item.ReorderLevel && before > item.ReorderLevel)
                {
                    await _notifications.CreateNotificationAsync(
                        "Low Stock Alert",
                        $"\"{item.Name}\" dropped to {after:0.##} {item.Unit} (reorder level: {item.ReorderLevel:0.##}).",
                        "Warning", NotificationCategory.Inventory,
                        branchId: item.BranchId, createdBy: null,
                        redirectUrl: "/InventoryItem/LowStock", icon: "fas fa-triangle-exclamation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Low-stock notification failed for item {ItemId}", item.Id);
            }
        }
    }
}
