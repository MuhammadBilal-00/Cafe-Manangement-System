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
            var q = _context.MenuItems.Include(m => m.Category)
                .Where(m => m.BranchId == branchId && m.Availability);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(m => m.Name.Contains(search) || (m.Sku != null && m.Sku == search));

            var items = await q.OrderBy(m => m.Category.Name).ThenBy(m => m.Name)
                .Select(m => new { id = m.Id, name = m.Name, price = m.Price, category = m.Category.Name, sku = m.Sku, available = m.Availability })
                .ToListAsync();
            return Json(items);
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

            decimal subtotal = 0;
            foreach (var line in req.Items)
            {
                var mi = await _context.MenuItems.FirstOrDefaultAsync(m => m.Id == line.MenuItemId && m.BranchId == req.BranchId);
                if (mi == null) continue;
                var qty = Math.Max(1, line.Quantity);
                subtotal += (mi.Price * qty) - Math.Clamp(line.LineDiscount, 0, mi.Price * qty);
            }

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
