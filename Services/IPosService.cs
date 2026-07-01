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
        private readonly IMemoryCache _cache;
        private readonly ILogger<PosService> _logger;

        public PosService(ApplicationDbContext db, IInventoryService inventory, IInvoiceService invoices,
            IBranchSettingService branchSettings, IMemoryCache cache, ILogger<PosService> logger)
        {
            _db = db;
            _inventory = inventory;
            _invoices = invoices;
            _branchSettings = branchSettings;
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
            var order = await BuildOrderAsync(req);
            if (order.items.Count == 0)
                return new PosSaleResult(false, "No valid items for this branch.");

            var entity = order.entity;
            entity.HoldState = "Active";
            entity.KitchenStatus = "New";
            entity.Status = "Pending";
            await _db.SaveChangesAsync();

            // Atomic stock deduction (InventoryService uses a conditional-update transaction).
            var deducted = await _inventory.DeductInventoryForOrder(entity.Id, req.BranchId, userName);
            if (!deducted)
            {
                // Only tear down a brand-new order; a resumed draft is left intact for retry.
                if (order.isNew)
                {
                    _db.OrderItems.RemoveRange(entity.OrderItems);
                    _db.Orders.Remove(entity);
                    await _db.SaveChangesAsync();
                }
                return new PosSaleResult(false, "Insufficient stock — availability changed. Please review the cart.");
            }

            // Invoice (server re-validates promo/partnership; can't be tampered client-side).
            var setting = await _branchSettings.GetOrCreateAsync(req.BranchId);
            var primaryMethod = req.Payments.FirstOrDefault()?.Method ?? "Cash";
            var invoice = await _invoices.CreateForOrderAsync(entity.Id, req.PromoCode, req.PartnershipId,
                primaryMethod, "Pending", userId, req.TaxRateOverride);

            // Record split payments directly (single SaveChanges) and derive PaymentStatus.
            decimal paid = 0;
            foreach (var p in req.Payments.Where(p => p.Amount > 0))
            {
                _db.Payments.Add(new Payment
                {
                    InvoiceId = invoice.Id,
                    Method = string.IsNullOrWhiteSpace(p.Method) ? "Cash" : p.Method,
                    Amount = Math.Round(p.Amount, 2),
                    Reference = p.Reference,
                    PaidAt = DateTime.Now
                });
                paid += Math.Round(p.Amount, 2);
            }

            var fullyPaid = paid + 0.01m >= invoice.TotalAmount && paid > 0;
            // With a hardware terminal we wait for the webhook; otherwise the tenders close the bill.
            invoice.PaymentStatus = fullyPaid && !setting.HardwareTerminalEnabled ? "Paid" : "Pending";
            if (invoice.PaymentStatus == "Paid") invoice.PaidAt = DateTime.Now;

            // Order amount = net sales (after discounts, before tax) for correct revenue.
            entity.TotalAmount = invoice.Subtotal - invoice.TotalDiscount;
            await _db.SaveChangesAsync();

            if (req.TableId.HasValue)
                await _db.RestaurantTables.Where(t => t.Id == req.TableId.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "Occupied"));

            if (!string.IsNullOrWhiteSpace(req.ClientRef))
                _cache.Set(dedupeKey, true, TimeSpan.FromSeconds(20));

            var change = Math.Max(0, paid - invoice.TotalAmount);
            return new PosSaleResult(true, fullyPaid ? "Sale complete." : "Sale recorded — balance due.",
                entity.Id, entity.OrderNumber, invoice.InvoiceNumber, invoice.TotalAmount, paid, change,
                invoice.PaymentStatus, invoice.PdfPath);
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
