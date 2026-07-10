using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cafe.Services
{
    public record PosSaleResult(bool Success, string Message, int? OrderId = null, string? OrderNumber = null,
        string? InvoiceNumber = null, decimal Total = 0, decimal Paid = 0, decimal Change = 0,
        string PaymentStatus = "", string? PdfUrl = null);

    public interface IPosService
    {
        /// <summary>Finalize a sale: create/replace the order + items, atomically deduct stock,
        /// generate the invoice (promo/partnership/packing/shipping/editable-tax) and record split
        /// payments. Idempotent via the client ref. On stock failure the order is rolled back.</summary>
        Task<PosSaleResult> FinalizeAsync(PosSaleRequest req, int? userId, string userName);

        /// <summary>Persist a Suspended (hold) or Draft order — no stock deduction, no invoice.</summary>
        Task<PosSaleResult> SaveHoldOrDraftAsync(PosSaleRequest req, string holdState, int? userId);

        /// <summary>
        /// Cancel an order together with its financial and stock side effects: void the unpaid
        /// invoice, mirror-reverse its ledger journal, restock ingredients when the kitchen never
        /// started, and free the table. Paid/partially-paid orders are refused — money already
        /// taken must be refunded through a Sell Return, never silently voided.
        /// </summary>
        Task<(bool ok, string message)> CancelSaleAsync(int orderId, string? reason, int? userId, string userName);

        /// <summary>Resume a held/draft order back to Active and return it (with items) for the cart.</summary>
        Task<Order?> ResumeAsync(int orderId);

        Task<List<Order>> ListByHoldStateAsync(int branchId, string holdState);
        Task<List<object>> RecentTransactionsAsync(int branchId, int take = 15);

        /// <summary>Authoritative cart subtotal incl. server-validated modifier deltas &amp; line discounts.</summary>
        Task<decimal> CartSubtotalAsync(PosSaleRequest req);
    }

    public class PosService : IPosService
    {
        private readonly ApplicationDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IInvoiceService _invoices;
        private readonly IBranchSettingService _branchSettings;
        private readonly ILoyaltyService _loyalty;
        private readonly IGiftCardService _giftCards;
        private readonly IAccountingService _accounting;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PosService> _logger;

        public PosService(ApplicationDbContext db, IInventoryService inventory, IInvoiceService invoices,
            IBranchSettingService branchSettings, ILoyaltyService loyalty, IGiftCardService giftCards,
            IAccountingService accounting, IMemoryCache cache, ILogger<PosService> logger)
        {
            _db = db;
            _inventory = inventory;
            _invoices = invoices;
            _branchSettings = branchSettings;
            _loyalty = loyalty;
            _giftCards = giftCards;
            _accounting = accounting;
            _cache = cache;
            _logger = logger;
        }

        public async Task<PosSaleResult> FinalizeAsync(PosSaleRequest req, int? userId, string userName)
        {
            if (req.Items == null || req.Items.Count == 0)
                return new PosSaleResult(false, "The cart is empty.");

            // Idempotency: identical client ref within a short window = duplicate submit.
            var dedupeKey = $"pos-finalize:{userId}:{req.BranchId}:{req.ClientRef}";
            if (!string.IsNullOrWhiteSpace(req.ClientRef) && _cache.TryGetValue(dedupeKey, out _))
                return new PosSaleResult(false, "This sale was just submitted.");

            // Resolve a validated order + items (create new, or reuse a resumed hold/draft).
            // Saved as Pending first: it only becomes "Preparing" once stock is secured, so a
            // deduction failure never leaves a fired-looking order behind.
            var order = await BuildOrderAsync(req);
            if (order.items.Count == 0)
                return new PosSaleResult(false, "No valid items for this branch.");

            var entity = order.entity;
            await SaveOrderWithNumberRetryAsync(entity, req.BranchId, order.isNew);

            // Atomic stock deduction (InventoryService uses a conditional-update transaction).
            var deducted = await _inventory.DeductInventoryForOrder(entity.Id, req.BranchId, userName);
            if (!deducted)
            {
                // Only tear down a brand-new order; a resumed hold/draft stays Pending for retry.
                if (order.isNew)
                {
                    _db.OrderItems.RemoveRange(entity.OrderItems);
                    _db.Orders.Remove(entity);
                    await _db.SaveChangesAsync();
                }
                return new PosSaleResult(false, "Insufficient stock — availability changed. Please review the cart.");
            }

            // From here on the stock is spent. Any failure below compensates: restock via the
            // ledger and put the order back to Pending, so a crashed checkout can be retried
            // without double-deducting or losing inventory.
            try
            {
                entity.HoldState = "Active";
                entity.KitchenStatus = "New";
                // A committed POS sale is fired to the kitchen the moment it exists, so it is born
                // "Preparing" — KOT printing is best-effort after. "Pending" is reserved for
                // orders NOT yet fired (held/suspended/drafts).
                entity.Status = "Preparing";

                // Invoice (server re-validates promo/partnership; can't be tampered client-side).
                var setting = await _branchSettings.GetOrCreateAsync(req.BranchId);
                var primaryMethod = req.Payments.FirstOrDefault()?.Method ?? "Cash";
                var invoice = await _invoices.CreateForOrderAsync(entity.Id, req.PromoCode, req.PartnershipId,
                    primaryMethod, "Pending", userId, req.TaxRateOverride);

                // Record split payments and derive PaymentStatus. Every tender is capped at the
                // amount still due: a gift card can never be redeemed past the bill (no cash-out
                // through the register) and cash change handed back is not booked as a payment —
                // otherwise Payments would overstate takings and drive receivables negative.
                decimal recorded = 0;   // what lands in Payments (≤ invoice total)
                decimal tendered = 0;   // what the customer physically handed over
                foreach (var p in req.Payments.Where(p => p.Amount > 0))
                {
                    var method = string.IsNullOrWhiteSpace(p.Method) ? "Cash" : p.Method.Trim();
                    var requested = Math.Round(p.Amount, 2);
                    var due = Math.Max(0, invoice.TotalAmount - recorded);
                    var amount = Math.Min(requested, due);
                    var reference = p.Reference;

                    if (method.Equals("GiftCard", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(p.Reference) || amount <= 0) continue;
                        var gr = await _giftCards.RedeemAsync(p.Reference.Trim(), amount, invoice.Id);
                        if (!gr.Success || gr.Applied <= 0) continue;       // invalid / empty card → contributes nothing
                        amount = gr.Applied;                                 // only what the card actually covered
                        reference = p.Reference.Trim();
                        tendered += amount;                                  // a card tender never produces change
                    }
                    else if (method.Equals("Loyalty", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!entity.CustomerId.HasValue || amount <= 0) continue; // walk-in has no points
                        var points = (int)Math.Floor(amount);                     // 1 point = Rs.1
                        if (points <= 0) continue;
                        var lr = await _loyalty.RedeemAsync(entity.CustomerId.Value, points, invoice.Id);
                        if (!lr.ok) continue;                                     // insufficient points → tender ignored
                        amount = points;
                        reference = $"{points} pts";
                        tendered += amount;
                    }
                    else
                    {
                        tendered += requested;                               // cash/card: full hand-over counts toward change
                        if (amount <= 0) continue;                           // bill already covered → pure change
                    }

                    _db.Payments.Add(new Payment
                    {
                        InvoiceId = invoice.Id,
                        Method = method,
                        Amount = amount,
                        Reference = reference,
                        PaidAt = DateTime.Now
                    });
                    recorded += amount;
                }

                // A zero-total bill (fully discounted/comped) is settled by definition.
                var fullyPaid = invoice.TotalAmount <= 0 || (recorded + 0.01m >= invoice.TotalAmount && recorded > 0);
                // With a hardware terminal we wait for the webhook; otherwise the tenders close the bill.
                invoice.PaymentStatus = fullyPaid && !setting.HardwareTerminalEnabled ? "Paid" : "Pending";
                if (invoice.PaymentStatus == "Paid") invoice.PaidAt = DateTime.Now;

                // Order amount = net sales (after discounts, before tax) for correct revenue.
                entity.TotalAmount = invoice.Subtotal - invoice.TotalDiscount;
                await _db.SaveChangesAsync();

                // Phase 6: earn loyalty points when a customer's sale is fully paid (idempotent per invoice).
                if (invoice.PaymentStatus == "Paid" && entity.CustomerId.HasValue)
                {
                    try { await _loyalty.EarnForInvoiceAsync(invoice); } catch (Exception ex) { _logger.LogWarning(ex, "Loyalty earn failed"); }
                }

                if (req.TableId.HasValue)
                    await _db.RestaurantTables.Where(t => t.Id == req.TableId.Value)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "Occupied"));

                if (!string.IsNullOrWhiteSpace(req.ClientRef))
                    _cache.Set(dedupeKey, true, TimeSpan.FromSeconds(20));

                var change = Math.Max(0, tendered - recorded);
                return new PosSaleResult(true, fullyPaid ? "Sale complete." : "Sale recorded — balance due.",
                    entity.Id, entity.OrderNumber, invoice.InvoiceNumber, invoice.TotalAmount, recorded, change,
                    invoice.PaymentStatus, invoice.PdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POS finalize failed after stock deduction for order {OrderId} — compensating", entity.Id);
                try
                {
                    await _inventory.RestockOrderAsync(entity.Id, "Checkout failed — stock returned", userName);
                    entity.Status = "Pending";
                    entity.HoldState = order.isNew ? "Draft" : entity.HoldState;
                    await _db.SaveChangesAsync();
                }
                catch (Exception cex)
                {
                    _logger.LogCritical(cex, "Compensation failed for order {OrderId} — stock may need a manual adjustment", entity.Id);
                }
                return new PosSaleResult(false, "Checkout failed — nothing was charged. The cart was kept as a draft; please retry.");
            }
        }

        /// <summary>
        /// Save a new/reused order, retrying once with a regenerated number if a concurrent
        /// register grabbed the same sequence (the per-tenant unique index is the referee).
        /// </summary>
        private async Task SaveOrderWithNumberRetryAsync(Order entity, int branchId, bool isNew)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _db.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException ex) when (isNew && attempt < 3 && IsUniqueViolation(ex))
                {
                    entity.OrderNumber = await GenerateOrderNumberAsync(branchId);
                }
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

        public async Task<(bool ok, string message)> CancelSaleAsync(int orderId, string? reason, int? userId, string userName)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return (false, "Order not found.");
            if (order.Status == "Cancelled") return (true, "Order is already cancelled.");
            if (order.Status == "Completed") return (false, "Completed orders can't be cancelled — process a Sell Return instead.");

            var invoice = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.OrderId == orderId);
            if (invoice != null)
            {
                // Money already taken can't be silently voided — the refund (cash back, stock in,
                // AR credit) belongs to the Sell Return flow where it is explicit and auditable.
                if (invoice.PaymentStatus == "Paid" || invoice.Payments.Sum(p => p.Amount) > 0)
                    return (false, "This bill has payments against it. Refund it through a Sell Return instead of cancelling.");

                invoice.PaymentStatus = "Cancelled";
                // If the nightly/manual auto-post already journaled this bill as a receivable,
                // mirror-reverse it so the ledger nets to zero for this sale.
                try { await _accounting.ReverseInvoiceJournalAsync(invoice.Id, userId); }
                catch (Exception ex) { _logger.LogError(ex, "Invoice reversal failed for invoice {InvoiceId}", invoice.Id); }
            }

            // Ingredients come back only if the kitchen never started this ticket. Once cooking
            // began they are physically consumed — the Order Usage ledger rows stay attributed
            // to this order as traceable wastage.
            var kitchenUntouched = order.KitchenStatus == "New";
            var hadStockDeducted = await _db.InventoryTransactions
                .AnyAsync(t => t.OrderId == orderId && t.TransactionType == "Order Usage");
            if (kitchenUntouched && hadStockDeducted)
                await _inventory.RestockOrderAsync(orderId, "Order cancelled before preparation", userName);

            order.Status = "Cancelled";
            OrderWorkflow.SyncKitchenFromOrder(order); // closes the ticket so the KDS drops it
            if (!string.IsNullOrWhiteSpace(reason))
                order.Notes = string.IsNullOrEmpty(order.Notes) ? $"Cancelled: {reason}" : $"{order.Notes}\nCancelled: {reason}";

            // A cancelled dine-in frees its table for the next guests.
            if (order.TableId.HasValue)
                await _db.RestaurantTables.Where(t => t.Id == order.TableId.Value && t.Status == "Occupied")
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "Available"));

            await _db.SaveChangesAsync();

            var details = !hadStockDeducted ? ""
                : kitchenUntouched ? " Ingredients were returned to stock."
                : " Ingredients already in preparation were recorded as wastage.";
            return (true, $"Order cancelled.{(invoice != null ? " The bill was voided." : "")}{details}");
        }

        public async Task<PosSaleResult> SaveHoldOrDraftAsync(PosSaleRequest req, string holdState, int? userId)
        {
            if (req.Items == null || req.Items.Count == 0)
                return new PosSaleResult(false, "The cart is empty.");

            var order = await BuildOrderAsync(req);
            if (order.items.Count == 0)
                return new PosSaleResult(false, "No valid items for this branch.");

            order.entity.HoldState = holdState;       // Suspended | Draft
            order.entity.Status = "Pending";
            order.entity.KitchenStatus = "New";
            // Net subtotal for display; no stock deducted and no invoice until finalised.
            order.entity.TotalAmount = order.items.Sum(i => (i.Price * i.Quantity) - i.LineDiscount);
            await _db.SaveChangesAsync();

            return new PosSaleResult(true, holdState == "Draft" ? "Draft saved." : "Order held.",
                order.entity.Id, order.entity.OrderNumber);
        }

        public async Task<Order?> ResumeAsync(int orderId)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return null;
            order.HoldState = "Active";
            await _db.SaveChangesAsync();
            return order;
        }

        public Task<List<Order>> ListByHoldStateAsync(int branchId, string holdState) =>
            _db.Orders
                .Where(o => o.BranchId == branchId && o.HoldState == holdState
                    && o.Status != "Completed" && o.Status != "Cancelled")
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

        public async Task<List<object>> RecentTransactionsAsync(int branchId, int take = 15)
        {
            var rows = await _db.Invoices
                .Where(i => i.BranchId == branchId)
                .OrderByDescending(i => i.CreatedAt)
                .Take(take)
                .Select(i => new
                {
                    i.InvoiceNumber,
                    orderNumber = i.Order.OrderNumber,
                    i.TotalAmount,
                    i.PaymentStatus,
                    i.PaymentMethod,
                    createdAt = i.CreatedAt,
                    pdfUrl = i.PdfPath
                })
                .ToListAsync();
            return rows.Cast<object>().ToList();
        }

        public async Task<decimal> CartSubtotalAsync(PosSaleRequest req)
        {
            decimal sub = 0;
            foreach (var line in req.Items)
            {
                var mi = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == line.MenuItemId && m.BranchId == req.BranchId);
                if (mi == null) continue;
                var (unit, _) = await ResolveLineAsync(mi, line.ModifierIds, req.PriceGroupId);
                var qty = Math.Max(1, line.Quantity);
                sub += (unit * qty) - Math.Clamp(line.LineDiscount, 0, unit * qty);
            }
            return sub;
        }

        /// <summary>Unit price = tier base price (price-group override or item base) + validated modifier deltas.</summary>
        private async Task<(decimal unitPrice, string? modNote)> ResolveLineAsync(MenuItem mi, List<int>? modifierIds, int? priceGroupId)
        {
            var basePrice = await EffectiveBaseAsync(mi.Id, mi.Price, priceGroupId);
            decimal delta = 0;
            string? note = null;
            if (modifierIds is { Count: > 0 })
            {
                var mods = await _db.Modifiers.Where(m => modifierIds.Contains(m.Id) && m.IsActive).ToListAsync();
                delta = mods.Sum(m => m.PriceDelta);
                if (mods.Count > 0) note = string.Join(", ", mods.Select(m => m.Name));
            }
            return (basePrice + delta, note);
        }

        /// <summary>Effective base price for a menu item under an optional price group (override or base).</summary>
        private async Task<decimal> EffectiveBaseAsync(int menuItemId, decimal basePrice, int? priceGroupId)
        {
            if (priceGroupId is null) return basePrice;
            var ov = await _db.MenuItemPrices
                .Where(p => p.MenuItemId == menuItemId && p.PriceGroupId == priceGroupId.Value)
                .Select(p => (decimal?)p.Price).FirstOrDefaultAsync();
            return ov ?? basePrice;
        }

        // ── helpers ──

        private async Task<(Order entity, List<OrderItem> items, bool isNew)> BuildOrderAsync(PosSaleRequest req)
        {
            Order entity;
            bool isNew;
            if (req.ExistingOrderId.HasValue)
            {
                entity = await _db.Orders.Include(o => o.OrderItems)
                    .FirstAsync(o => o.Id == req.ExistingOrderId.Value);
                _db.OrderItems.RemoveRange(entity.OrderItems); // replace lines with the current cart
                isNew = false;
            }
            else
            {
                entity = new Order { OrderNumber = await GenerateOrderNumberAsync(req.BranchId), OrderDate = DateTime.Now };
                _db.Orders.Add(entity);
                isNew = true;
            }

            entity.BranchId = req.BranchId;
            entity.CustomerId = req.CustomerId;             // null = walk-in
            entity.TableId = req.TableId;
            entity.ServiceType = Sanitize(req.ServiceType, new[] { "DineIn", "Takeaway", "Delivery" }, "DineIn");
            entity.ServiceStaffId = req.ServiceStaffId;
            entity.Notes = req.Notes;
            entity.PackingCharge = Math.Max(0, req.PackingCharge);
            entity.ShippingCharge = Math.Max(0, req.ShippingCharge);

            // Validate each line against this branch's live menu; price is taken server-side.
            var items = new List<OrderItem>();
            foreach (var line in req.Items)
            {
                var mi = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == line.MenuItemId
                    && m.BranchId == req.BranchId && m.Availability);
                if (mi == null) continue;
                var qty = Math.Max(1, line.Quantity);
                // Server-side price math: tier base price + validated modifier deltas.
                var (unitPrice, modNote) = await ResolveLineAsync(mi, line.ModifierIds, req.PriceGroupId);
                var lineTotal = unitPrice * qty;
                var discount = Math.Clamp(line.LineDiscount, 0, lineTotal); // never below zero
                var notes = string.Join(" · ", new[] { modNote, line.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                items.Add(new OrderItem
                {
                    Order = entity,
                    MenuItemId = mi.Id,
                    Quantity = qty,
                    Price = unitPrice,
                    LineDiscount = Math.Round(discount, 2),
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                });
            }
            _db.OrderItems.AddRange(items);
            entity.OrderItems = items;

            // Provisional total (finalise recomputes from the invoice). Never 0 for the CK constraint.
            entity.TotalAmount = items.Sum(i => (i.Price * i.Quantity) - i.LineDiscount);
            return (entity, items, isNew);
        }

        private static string Sanitize(string? value, string[] allowed, string fallback) =>
            allowed.Contains(value) ? value! : fallback;

        private async Task<string> GenerateOrderNumberAsync(int branchId)
        {
            var branch = await _db.Branches.FindAsync(branchId);
            var code = new string((branch?.Name ?? "ORD").Where(char.IsLetter).Take(3).ToArray()).ToUpper();
            if (code.Length == 0) code = "ORD";
            var today = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"{code}{today}";
            var count = await _db.Orders.CountAsync(o => o.OrderNumber.StartsWith(prefix));
            for (var seq = count + 1; ; seq++)
            {
                var candidate = $"{prefix}{seq:D3}";
                if (!await _db.Orders.AnyAsync(o => o.OrderNumber == candidate))
                    return candidate;
            }
        }
    }
}
