using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Hubs;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Kitchen Display System (Phase 1) + KOT printing. Real-time ticket board via SignalR.</summary>
    [RequireStaffOrAbove]
    public class KitchenController : BaseController
    {
        private readonly IKitchenService _kitchen;
        private readonly IHubContext<KitchenHub> _hub;
        private readonly IAuditLogService _audit;
        private readonly IKotPrintService _kot;

        public KitchenController(ApplicationDbContext context, IKitchenService kitchen,
            IHubContext<KitchenHub> hub, IAuditLogService audit, IKotPrintService kot) : base(context)
        {
            _kitchen = kitchen;
            _hub = hub;
            _audit = audit;
            _kot = kot;
        }

        [RequireFeature("KDS")]
        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;
            return View();
        }

        [HttpGet]
        [RequireFeature("KDS")]
        public async Task<IActionResult> GetTickets(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var tickets = await _kitchen.GetTicketsAsync(branchId);
            return Json(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireFeature("KDS")]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null || !CanAccessBranch(order.BranchId))
                return Json(new { success = false, message = "Order not found or access denied." });

            var ticket = await _kitchen.UpdateStatusAsync(orderId, status);
            if (ticket == null)
                return Json(new { success = false, message = "Invalid kitchen transition." });

            await _audit.LogAsync("StatusChange", "Order", orderId, $"Kitchen → {status}", order.BranchId);

            // Push the change to every KDS + register watching this branch (real-time, no refresh).
            await _hub.Clients.Group($"kitchen_branch_{ticket.BranchId}")
                .SendAsync("TicketUpdated", ticket);

            return Json(new { success = true, ticket });
        }

        // ── KOT printing (works whenever POS works; not gated behind KDS) ──

        /// <summary>Print-optimized 80mm KOT for a station (opened in a hidden iframe by the POS, or reprinted).</summary>
        [HttpGet]
        public async Task<IActionResult> Kot(int id, string? station, int? printer)
        {
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.ServiceStaff).ThenInclude(s => s!.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(m => m!.Category)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null || !CanAccessBranch(order.BranchId)) return NotFound();

            var printers = await _context.KitchenPrinters
                .Where(p => p.BranchId == order.BranchId && p.IsActive).ToListAsync();
            var pr = printer.HasValue ? printers.FirstOrDefault(x => x.Id == printer.Value) : null;
            var stationName = pr?.Station ?? (string.IsNullOrWhiteSpace(station) ? "Kitchen" : station!);
            var isDefault = pr?.IsDefault ?? false;

            IEnumerable<Cafe.Models.OrderItem> items = order.OrderItems;
            if (printers.Any())
            {
                items = items.Where(oi =>
                {
                    var cs = oi.MenuItem?.Category?.KotStation;
                    return !string.IsNullOrWhiteSpace(cs)
                        ? string.Equals(cs, stationName, StringComparison.OrdinalIgnoreCase)
                        : isDefault; // unrouted items ride on the default printer
                });
            }

            var header = new KitchenTicketHeader(order.OrderNumber, order.Table?.Name, order.ServiceType,
                order.ServiceStaff?.User?.Name, DateTime.Now);
            var lines = items.Select(oi => new KotLine(oi.MenuItem?.Name ?? "Item", oi.Quantity, oi.Notes)).ToList();
            return View("Kot", new KotSlipVm(stationName, header, lines));
        }

        /// <summary>Test slip for a browser printer.</summary>
        [HttpGet]
        public async Task<IActionResult> KotTest(int id)
        {
            var p = await _context.KitchenPrinters.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null || !CanAccessBranch(p.BranchId)) return NotFound();
            var header = new KitchenTicketHeader("TEST-0001", "T1", "DineIn", "Test Waiter", DateTime.Now);
            var lines = new List<KotLine> { new("Test Item A", 2, "no onions"), new("Test Item B", 1, "extra spicy · Large") };
            return View("Kot", new KotSlipVm(p.Station, header, lines));
        }

        /// <summary>Manual reprint of an order's KOT(s) — from the KDS or the register.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReprintKot(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null || !CanAccessBranch(order.BranchId))
                return Json(new { success = false, message = "Order not found." });
            var r = await _kot.PrintForOrderAsync(orderId, isReprint: true);
            await _audit.LogAsync("Reprint", "Order", orderId, "KOT reprinted", order.BranchId);
            return Json(new { success = true, browserKots = r.BrowserKots, warnings = r.Warnings, networkPrinted = r.NetworkPrinted });
        }
    }
}
