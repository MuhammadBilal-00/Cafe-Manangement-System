using System.Text.Json;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireFeature("Inventory")]
    [RequireManagerOrOwner]
    public class StockAdjustmentController : BaseController
    {
        private readonly ISupplyChainService _supply;
        private readonly IAuditLogService _audit;

        public StockAdjustmentController(ApplicationDbContext context, ISupplyChainService supply, IAuditLogService audit) : base(context)
        {
            _supply = supply;
            _audit = audit;
        }

        private record Line(int inventoryItemId, decimal quantityDelta);

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var adjustments = await _context.StockAdjustments
                .Include(a => a.Branch).Include(a => a.Lines)
                .Where(a => branchIds.Contains(a.BranchId))
                .OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
            ViewBag.Branches = await GetAccessibleBranches();
            return View(adjustments);
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var items = await _context.InventoryItems.Where(i => i.BranchId == branchId)
                .OrderBy(i => i.Name).Select(i => new { id = i.Id, name = i.Name, unit = i.Unit, qty = i.Quantity }).ToListAsync();
            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int branchId, string type, string reason, string lines)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (string.IsNullOrWhiteSpace(reason)) return Json(new { success = false, message = "A reason is required." });
            if (type is not ("Increase" or "Decrease" or "Recount")) type = "Decrease";

            List<Line> parsed;
            try { parsed = JsonSerializer.Deserialize<List<Line>>(lines ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid lines." }); }
            parsed = parsed.Where(l => l.inventoryItemId > 0 && l.quantityDelta != 0).ToList();
            if (parsed.Count == 0) return Json(new { success = false, message = "Add at least one line with a non-zero change." });

            var adj = new StockAdjustment
            {
                BranchId = branchId, Type = type, Reason = reason.Trim(), ApprovalStatus = "Pending",
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            };
            _context.StockAdjustments.Add(adj);
            await _context.SaveChangesAsync();
            foreach (var l in parsed)
                _context.StockAdjustmentLines.Add(new StockAdjustmentLine { StockAdjustmentId = adj.Id, InventoryItemId = l.inventoryItemId, QuantityDelta = l.quantityDelta });
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Create", "StockAdjustment", adj.Id, $"Draft adjustment ({type}) — {reason}", branchId);
            return Json(new { success = true, id = adj.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var adj = await _context.StockAdjustments.FirstOrDefaultAsync(a => a.Id == id);
            if (adj == null || !CanAccessBranch(adj.BranchId)) return Json(new { success = false, message = "Not found." });
            var (ok, msg) = await _supply.ApproveAdjustmentAsync(id, GetCurrentUserId() ?? 0, HttpContext.Session.GetUserName() ?? "System");
            if (ok) await _audit.LogAsync("Approve", "StockAdjustment", id, "Adjustment applied", adj.BranchId);
            return Json(new { success = ok, message = msg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var adj = await _context.StockAdjustments.FirstOrDefaultAsync(a => a.Id == id);
            if (adj == null || !CanAccessBranch(adj.BranchId)) return Json(new { success = false });
            if (adj.ApprovalStatus != "Pending") return Json(new { success = false, message = "Already resolved." });
            adj.ApprovalStatus = "Rejected"; adj.ApprovedById = GetCurrentUserId(); adj.ApprovedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
