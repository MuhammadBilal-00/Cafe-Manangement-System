using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 3 stock movements — transfers, adjustments, production. Every quantity change uses an
    /// atomic conditional UPDATE (deducts only when enough stock exists) inside one transaction, and
    /// writes an InventoryTransaction audit row. Stock can never go negative or be lost mid-move.
    /// </summary>
    public interface ISupplyChainService
    {
        Task<(bool ok, string message)> CompleteTransferAsync(int transferId, string performedBy);
        Task<(bool ok, string message)> ApproveAdjustmentAsync(int adjustmentId, int approverId, string performedBy);
        Task<(bool ok, string message)> CompleteProductionAsync(int productionId, string performedBy);

        /// <summary>Phase 4: approve a sell return — restock the returned goods (AR credit is derived).</summary>
        Task<(bool ok, string message)> ApproveSellReturnAsync(int sellReturnId, int approverId, string performedBy);
        /// <summary>Phase 4: approve a purchase return — remove the returned goods (AP is reduced).</summary>
        Task<(bool ok, string message)> ApprovePurchaseReturnAsync(int purchaseReturnId, int approverId, string performedBy);
    }

    public class SupplyChainService : ISupplyChainService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SupplyChainService> _logger;

        public SupplyChainService(ApplicationDbContext db, ILogger<SupplyChainService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool, string)> CompleteTransferAsync(int transferId, string performedBy)
        {
            var transfer = await _db.StockTransfers
                .Include(t => t.Items).ThenInclude(i => i.InventoryItem)
                .FirstOrDefaultAsync(t => t.Id == transferId);
            if (transfer == null) return (false, "Transfer not found.");
            if (transfer.Status != "Draft") return (false, "Only draft transfers can be completed.");
            if (transfer.FromBranchId == transfer.ToBranchId) return (false, "Source and destination branches must differ.");
            if (!transfer.Items.Any()) return (false, "Add at least one item to transfer.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var line in transfer.Items)
                {
                    var src = line.InventoryItem;
                    if (src == null || src.BranchId != transfer.FromBranchId)
                        { await tx.RollbackAsync(); return (false, "A line references an item not in the source branch."); }
                    if (line.Quantity <= 0) continue;

                    // Deduct from source (atomic — fails if not enough stock).
                    if (!await MoveAsync(src.Id, -line.Quantity, "Transfer Out", $"To {transfer.ToBranchId} (#{transfer.Id})", performedBy))
                        { await tx.RollbackAsync(); return (false, $"Insufficient stock of {src.Name} at the source branch."); }

                    // Find (or create) the matching item in the destination branch, then add.
                    var dest = await _db.InventoryItems.FirstOrDefaultAsync(i =>
                        i.BranchId == transfer.ToBranchId && i.Name == src.Name && i.Unit == src.Unit);
                    if (dest == null)
                    {
                        dest = new InventoryItem
                        {
                            Name = src.Name, Unit = src.Unit, BranchId = transfer.ToBranchId,
                            UnitPrice = src.UnitPrice, ReorderLevel = src.ReorderLevel, Quantity = 0,
                            SupplierId = null, LastUpdated = DateTime.Now
                        };
                        _db.InventoryItems.Add(dest);
                        await _db.SaveChangesAsync();
                    }
                    await MoveAsync(dest.Id, line.Quantity, "Transfer In", $"From {transfer.FromBranchId} (#{transfer.Id})", performedBy);
                }

                transfer.Status = "Completed";
                transfer.CompletedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, "Transfer completed.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Transfer {Id} failed", transferId);
                return (false, "Transfer failed — no stock was moved.");
            }
        }

        public async Task<(bool, string)> ApproveAdjustmentAsync(int adjustmentId, int approverId, string performedBy)
        {
            var adj = await _db.StockAdjustments
                .Include(a => a.Lines).ThenInclude(l => l.InventoryItem)
                .FirstOrDefaultAsync(a => a.Id == adjustmentId);
            if (adj == null) return (false, "Adjustment not found.");
            if (adj.ApprovalStatus != "Pending") return (false, "This adjustment is already resolved.");
            if (!adj.Lines.Any()) return (false, "Add at least one line.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var line in adj.Lines)
                {
                    if (line.InventoryItem == null || line.InventoryItem.BranchId != adj.BranchId)
                        { await tx.RollbackAsync(); return (false, "A line references an item not in this branch."); }
                    if (line.QuantityDelta == 0) continue;

                    if (!await MoveAsync(line.InventoryItemId, line.QuantityDelta, "Adjustment", adj.Reason, performedBy))
                        { await tx.RollbackAsync(); return (false, $"Insufficient stock of {line.InventoryItem.Name} for that decrease."); }
                }

                adj.ApprovalStatus = "Approved";
                adj.ApprovedById = approverId;
                adj.ApprovedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, "Adjustment approved and applied.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Adjustment {Id} failed", adjustmentId);
                return (false, "Adjustment failed — nothing was changed.");
            }
        }

        public async Task<(bool, string)> CompleteProductionAsync(int productionId, string performedBy)
        {
            var prod = await _db.ProductionOrders
                .Include(p => p.Inputs).ThenInclude(i => i.InventoryItem)
                .Include(p => p.OutputItem)
                .FirstOrDefaultAsync(p => p.Id == productionId);
            if (prod == null) return (false, "Production order not found.");
            if (prod.Status != "Draft") return (false, "Only draft orders can be completed.");
            if (prod.OutputItem == null || prod.OutputItem.BranchId != prod.BranchId) return (false, "Output item must be in this branch.");
            if (!prod.Inputs.Any()) return (false, "Add at least one input.");
            if (prod.OutputQuantity <= 0) return (false, "Output quantity must be positive.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                decimal totalCost = 0;
                foreach (var input in prod.Inputs)
                {
                    if (input.InventoryItem == null || input.InventoryItem.BranchId != prod.BranchId)
                        { await tx.RollbackAsync(); return (false, "An input is not in this branch."); }
                    if (input.Quantity <= 0) continue;

                    if (!await MoveAsync(input.InventoryItemId, -input.Quantity, "Production Consume", $"Production #{prod.Id}", performedBy))
                        { await tx.RollbackAsync(); return (false, $"Insufficient stock of {input.InventoryItem.Name}."); }

                    totalCost += input.Quantity * input.InventoryItem.UnitPrice; // cost roll-up
                }

                // Produce the output and roll the input cost into its manufactured unit cost.
                await MoveAsync(prod.OutputInventoryItemId, prod.OutputQuantity, "Production Output", $"Production #{prod.Id}", performedBy);
                var unitCost = Math.Round(totalCost / prod.OutputQuantity, 2);
                prod.TotalInputCost = Math.Round(totalCost, 2);
                prod.UnitCost = unitCost;
                prod.OutputItem.UnitPrice = unitCost; // manufactured cost becomes the output's cost basis
                prod.Status = "Completed";
                prod.CompletedAt = DateTime.Now;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, $"Produced {prod.OutputQuantity:0.##} @ Rs. {unitCost:N2}/unit (cost Rs. {totalCost:N2}).");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Production {Id} failed", productionId);
                return (false, "Production failed — no stock was changed.");
            }
        }

        public async Task<(bool, string)> ApproveSellReturnAsync(int sellReturnId, int approverId, string performedBy)
        {
            var ret = await _db.SellReturns.Include(r => r.Lines).ThenInclude(l => l.InventoryItem)
                .FirstOrDefaultAsync(r => r.Id == sellReturnId);
            if (ret == null) return (false, "Return not found.");
            if (ret.Status != "Pending") return (false, "This return is already resolved.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var line in ret.Lines)
                {
                    if (line.InventoryItem == null || line.InventoryItem.BranchId != ret.BranchId)
                        { await tx.RollbackAsync(); return (false, "A line references an item not in this branch."); }
                    if (line.Quantity > 0)
                        await MoveAsync(line.InventoryItemId, line.Quantity, "Sell Return", $"Sell return #{ret.Id}", performedBy);
                }
                ret.Status = "Approved"; ret.ApprovedById = approverId; ret.ApprovedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, "Sell return approved — goods restocked and customer credited.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Sell return {Id} failed", sellReturnId);
                return (false, "Return failed — nothing changed.");
            }
        }

        public async Task<(bool, string)> ApprovePurchaseReturnAsync(int purchaseReturnId, int approverId, string performedBy)
        {
            var ret = await _db.PurchaseReturns.Include(r => r.Lines).ThenInclude(l => l.InventoryItem)
                .FirstOrDefaultAsync(r => r.Id == purchaseReturnId);
            if (ret == null) return (false, "Return not found.");
            if (ret.Status != "Pending") return (false, "This return is already resolved.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var line in ret.Lines)
                {
                    if (line.InventoryItem == null || line.InventoryItem.BranchId != ret.BranchId)
                        { await tx.RollbackAsync(); return (false, "A line references an item not in this branch."); }
                    if (line.Quantity <= 0) continue;
                    if (!await MoveAsync(line.InventoryItemId, -line.Quantity, "Purchase Return", $"Purchase return #{ret.Id}", performedBy))
                        { await tx.RollbackAsync(); return (false, $"Insufficient stock of {line.InventoryItem.Name} to return."); }
                }
                ret.Status = "Approved"; ret.ApprovedById = approverId; ret.ApprovedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, "Purchase return approved — goods removed and payable reduced.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Purchase return {Id} failed", purchaseReturnId);
                return (false, "Return failed — nothing changed.");
            }
        }

        /// <summary>Atomic signed stock move + audit row. Returns false if a decrease would go negative.</summary>
        private async Task<bool> MoveAsync(int itemId, decimal delta, string type, string? notes, string performedBy)
        {
            var abs = Math.Abs(delta);
            int rows = delta < 0
                ? await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE InventoryItems SET Quantity = Quantity - {abs}, LastUpdated = GETDATE() WHERE Id = {itemId} AND Quantity >= {abs}")
                : await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE InventoryItems SET Quantity = Quantity + {abs}, LastUpdated = GETDATE() WHERE Id = {itemId}");
            if (rows == 0) return false;

            var item = await _db.InventoryItems.AsNoTracking().FirstAsync(i => i.Id == itemId);
            var after = item.Quantity;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryItemId = itemId,
                TransactionType = type,
                Quantity = abs,
                QuantityBefore = after - delta,
                QuantityAfter = after,
                Notes = notes,
                BranchId = item.BranchId,
                PerformedBy = performedBy,
                TransactionDate = DateTime.Now
            });
            return true;
        }
    }
}
