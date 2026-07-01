using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Hubs;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Cafe.Controllers
{
    /// <summary>Kitchen Display System (Phase 1). Real-time ticket board via SignalR — no polling.</summary>
    [RequireFeature("KDS")]
    [RequireStaffOrAbove]
    public class KitchenController : BaseController
    {
        private readonly IKitchenService _kitchen;
        private readonly IHubContext<KitchenHub> _hub;
        private readonly IAuditLogService _audit;

        public KitchenController(ApplicationDbContext context, IKitchenService kitchen,
            IHubContext<KitchenHub> hub, IAuditLogService audit) : base(context)
        {
            _kitchen = kitchen;
            _hub = hub;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetTickets(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var tickets = await _kitchen.GetTicketsAsync(branchId);
            return Json(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}
