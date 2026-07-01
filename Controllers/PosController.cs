using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Hubs;
using Cafe.Models;
using Cafe.Models.Requests;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Touch-first POS register (Phase 1). Cart, tile menu, split payments, hold/draft/resume,
    /// quick expense, recent transactions, and barcode scan. POS is a core module (no feature gate).
    /// </summary>
    [RequireStaffOrAbove]
    public class PosController : BaseController
    {
        private readonly IPosService _pos;
        private readonly ICheckoutPricingService _pricing;
        private readonly ITableService _tables;
        private readonly IKitchenService _kitchen;
        private readonly IInvoiceService _invoices;
        private readonly IHubContext<KitchenHub> _kitchenHub;
        private readonly INotificationService _notifications;
        private readonly ILogger<PosController> _logger;

        public PosController(ApplicationDbContext context, IPosService pos, ICheckoutPricingService pricing,
            ITableService tables, IKitchenService kitchen, IInvoiceService invoices,
            IHubContext<KitchenHub> kitchenHub, INotificationService notifications, ILogger<PosController> logger)
            : base(context)
        {
            _pos = pos;
            _pricing = pricing;
            _tables = tables;
            _kitchen = kitchen;
            _invoices = invoices;
            _kitchenHub = kitchenHub;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;
            return View();
        }

        // ── Reference data for the register ──

        [HttpGet]
        public async Task<IActionResult> GetMenu(int branchId, string? search)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var q = _context.MenuItems.Include(m => m.Category).Include(m => m.Brand)
                .Where(m => m.BranchId == branchId && m.Availability);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(m => m.Name.Contains(search) || (m.Sku != null && m.Sku == search));

            var rows = await q.OrderBy(m => m.Category.Name).ThenBy(m => m.Name).ToListAsync();

            // Phase 2: filter out items outside their time window / day mask right now.
            var now = DateTime.Now;
            var hasMods = await _context.MenuItemModifierGroups
                .Where(x => rows.Select(r => r.Id).Contains(x.MenuItemId))
                .Select(x => x.MenuItemId).Distinct().ToListAsync();

            var items = rows.Where(m => MenuAvailability.IsAvailable(m, now)).Select(m => new
            {
                id = m.Id, name = m.Name, price = m.Price, category = m.Category.Name,
                brand = m.Brand != null ? m.Brand.Name : null, sku = m.Sku, available = true,
                hasModifiers = hasMods.Contains(m.Id)
            });
            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> GetModifiers(int menuItemId)
        {
            var groups = await _context.MenuItemModifierGroups
                .Where(x => x.MenuItemId == menuItemId)
                .Join(_context.ModifierGroups.Where(g => g.IsActive), x => x.ModifierGroupId, g => g.Id, (x, g) => g)
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
                .Select(g => new
                {
                    id = g.Id, name = g.Name, min = g.MinSelect, max = g.MaxSelect, required = g.IsRequired,
                    options = _context.Modifiers.Where(o => o.ModifierGroupId == g.Id && o.IsActive)
                        .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
                        .Select(o => new { id = o.Id, name = o.Name, priceDelta = o.PriceDelta }).ToList()
                })
                .ToListAsync();
            return Json(groups);
        }

        [HttpGet]
        public async Task<IActionResult> GetCombos(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var combos = await _context.Combos
                .Where(c => c.BranchId == branchId && c.IsActive)
                .Select(c => new { c.Id, c.Name, c.Price }).ToListAsync();
            return Json(combos);
        }

        /// <summary>Expand a combo to its component menu-item lines so inventory deducts correctly.
        /// The discount (components' total − combo price) rides on the first line.</summary>
        [HttpGet]
        public async Task<IActionResult> ExpandCombo(int comboId)
        {
            var combo = await _context.Combos.Include(c => c.Items).ThenInclude(i => i.MenuItem)
                .FirstOrDefaultAsync(c => c.Id == comboId && c.IsActive);
            if (combo == null || !CanAccessBranch(combo.BranchId)) return NotFound();

            var lines = combo.Items.Where(i => i.MenuItem != null).Select(i => new
            {
                menuItemId = i.MenuItemId, name = i.MenuItem!.Name, price = i.MenuItem.Price, quantity = i.Quantity
            }).ToList();
            var componentsTotal = lines.Sum(l => l.price * l.quantity);
            var discount = Math.Max(0, componentsTotal - combo.Price);
            return Json(new { name = combo.Name, comboPrice = combo.Price, componentsTotal, discount, lines });
        }

        [HttpGet]
        public async Task<IActionResult> ScanLookup(int branchId, string code)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            if (string.IsNullOrWhiteSpace(code)) return Json(new { found = false });
            var m = await _context.MenuItems.FirstOrDefaultAsync(x =>
                x.BranchId == branchId && x.Availability && (x.Sku == code || x.Name == code));
            return m == null
                ? Json(new { found = false })
                : Json(new { found = true, id = m.Id, name = m.Name, price = m.Price });
        }

        [HttpGet]
        public async Task<IActionResult> GetTables(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var tables = await _tables.GetByBranchAsync(branchId);
            return Json(tables.Select(t => new { t.Id, t.Name, t.Zone, t.Status }));
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceStaff(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var staff = await _context.Staff.Include(s => s.User)
                .Where(s => s.BranchId == branchId && s.IsActive)
                .Select(s => new { id = s.Id, name = s.User.Name })
                .OrderBy(s => s.name).ToListAsync();
            return Json(staff);
        }

        [HttpGet]
        public async Task<IActionResult> GetPartnerships(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var ps = await _pricing.GetActivePartnershipsAsync(branchId);
            return Json(ps.Select(p => new { id = p.Id, name = $"{p.DisplayName} ({p.DiscountPercentage:0.##}%)" }));
        }

        /// <summary>Authoritative money breakdown for the current cart (server-computed).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Quote([FromBody] PosSaleRequest req)
        {
            if (!CanAccessBranch(req.BranchId)) return Forbid();

            // Server-authoritative subtotal incl. modifier deltas + line discounts.
            var subtotal = await _pos.CartSubtotalAsync(req);

            var pricing = await _pricing.ComputePricingAsync(req.BranchId, subtotal, req.PromoCode, req.PartnershipId,
                req.PackingCharge, req.ShippingCharge, req.TaxRateOverride);
            return Json(pricing);
        }

        // ── Sale actions ──

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalize([FromBody] PosSaleRequest req)
        {
            if (req == null || !CanAccessBranch(req.BranchId))
                return Json(new { success = false, message = "Access denied to this branch." });

            var result = await _pos.FinalizeAsync(req, GetCurrentUserId(), HttpContext.Session.GetUserName() ?? "System");
            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            // Push the new ticket to the KDS and notify — real-time, no refresh.
            try
            {
                var ticket = await _kitchen.GetTicketAsync(result.OrderId!.Value);
                if (ticket != null)
                    await _kitchenHub.Clients.Group($"kitchen_branch_{req.BranchId}").SendAsync("TicketUpdated", ticket);

                await _notifications.CreateNotificationAsync(
                    "New Sale", $"Order #{result.OrderNumber} — Rs. {result.Total:N0} ({result.PaymentStatus}).",
                    "Success", NotificationCategory.Order, branchId: req.BranchId, createdBy: GetCurrentUserId(),
                    redirectUrl: "/Kitchen/Index", icon: "fas fa-cash-register");
            }
            catch (Exception ex) { _logger.LogWarning(ex, "POS post-finalize push failed"); }

            return Json(new
            {
                success = true,
                message = result.Message,
                orderNumber = result.OrderNumber,
                invoiceNumber = result.InvoiceNumber,
                total = result.Total,
                paid = result.Paid,
                change = result.Change,
                paymentStatus = result.PaymentStatus,
                pdfUrl = result.PdfUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold([FromBody] PosSaleRequest req)
        {
            if (!CanAccessBranch(req.BranchId)) return Json(new { success = false, message = "Access denied." });
            var r = await _pos.SaveHoldOrDraftAsync(req, "Suspended", GetCurrentUserId());
            return Json(new { success = r.Success, message = r.Message, orderNumber = r.OrderNumber });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDraft([FromBody] PosSaleRequest req)
        {
            if (!CanAccessBranch(req.BranchId)) return Json(new { success = false, message = "Access denied." });
            var r = await _pos.SaveHoldOrDraftAsync(req, "Draft", GetCurrentUserId());
            return Json(new { success = r.Success, message = r.Message, orderNumber = r.OrderNumber });
        }

        [HttpGet]
        public async Task<IActionResult> Suspended(int branchId) => await ListHeld(branchId, "Suspended");
        [HttpGet]
        public async Task<IActionResult> Drafts(int branchId) => await ListHeld(branchId, "Draft");

        private async Task<IActionResult> ListHeld(int branchId, string holdState)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var list = await _pos.ListByHoldStateAsync(branchId, holdState);
            return Json(list.Select(o => new
            {
                id = o.Id, orderNumber = o.OrderNumber, total = o.TotalAmount,
                items = o.OrderItems.Count, serviceType = o.ServiceType, at = o.OrderDate.ToString("HH:mm")
            }));
        }

        [HttpGet]
        public async Task<IActionResult> Resume(int id)
        {
            var order = await _pos.ResumeAsync(id);
            if (order == null || !CanAccessBranch(order.BranchId)) return NotFound();
            return Json(new
            {
                id = order.Id,
                branchId = order.BranchId,
                customerId = order.CustomerId,
                tableId = order.TableId,
                serviceType = order.ServiceType,
                serviceStaffId = order.ServiceStaffId,
                notes = order.Notes,
                packingCharge = order.PackingCharge,
                shippingCharge = order.ShippingCharge,
                items = order.OrderItems.Select(oi => new
                {
                    menuItemId = oi.MenuItemId, name = oi.MenuItem?.Name, quantity = oi.Quantity,
                    price = oi.Price, lineDiscount = oi.LineDiscount, notes = oi.Notes
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int invoiceId, string method, decimal amount, string? reference)
        {
            var invoice = await _invoices.GetByIdAsync(invoiceId);
            if (invoice == null || !CanAccessBranch(invoice.BranchId))
                return Json(new { success = false, message = "Invoice not found." });

            var r = await _invoices.AddPaymentAsync(invoiceId, method, amount, reference);
            return Json(new { success = r.Success, message = r.Message, paid = r.TotalPaid, due = r.AmountDue, status = r.PaymentStatus });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickExpense(int branchId, string title, decimal amount, string? category)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (string.IsNullOrWhiteSpace(title) || amount <= 0)
                return Json(new { success = false, message = "Title and a positive amount are required." });

            _context.Expenses.Add(new Expense
            {
                BranchId = branchId,
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Misc" : category.Trim(),
                Amount = Math.Round(amount, 2),
                ExpenseDate = DateTime.Now,
                PaymentMethod = "Cash",
                ApprovalStatus = "Approved",
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Expense recorded." });
        }

        [HttpGet]
        public async Task<IActionResult> RecentTransactions(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            return Json(await _pos.RecentTransactionsAsync(branchId));
        }
    }
}
