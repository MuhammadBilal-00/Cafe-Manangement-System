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
    /// <summary>Phase 4: supplier returns — destock + reduce AP.</summary>
    [RequireFeature("Inventory")]
    [RequireManagerOrOwner]
    public class PurchaseReturnController : BaseController
    {
        private readonly ISupplyChainService _supply;
        private readonly IAuditLogService _audit;

        public PurchaseReturnController(ApplicationDbContext context, ISupplyChainService supply, IAuditLogService audit) : base(context)
        {
            _supply = supply;
            _audit = audit;
        }

        private record Line(int inventoryItemId, decimal quantity, decimal unitCost);

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var returns = await _context.PurchaseReturns.Include(r => r.Branch).Include(r => r.Supplier).Include(r => r.Lines)
                .Where(r => branchIds.Contains(r.BranchId)).OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();
            ViewBag.Branches = await GetAccessibleBranches();
            return View(returns);
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var items = await _context.InventoryItems.Where(i => i.BranchId == branchId)
                .OrderBy(i => i.Name).Select(i => new { id = i.Id, name = i.Name, unit = i.Unit, qty = i.Quantity, cost = i.UnitPrice }).ToListAsync();
            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            var sups = await GetAccessibleSuppliers();
            return Json(sups.Select(s => new { id = s.Id, name = s.Name }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int branchId, int? supplierId, string? reason, string lines)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            List<Line> parsed;
            try { parsed = JsonSerializer.Deserialize<List<Line>>(lines ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid lines." }); }
            parsed = parsed.Where(l => l.inventoryItemId > 0 && l.quantity > 0).ToList();
            if (parsed.Count == 0) return Json(new { success = false, message = "Add at least one line." });

            var ret = new PurchaseReturn
            {
                BranchId = branchId, SupplierId = supplierId, ReturnNumber = await GenAsync("PR", _context.PurchaseReturns.Select(x => x.ReturnNumber)),
                Status = "Pending", TotalAmount = Math.Round(parsed.Sum(l => l.quantity * l.unitCost), 2), Reason = reason,
                CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            };
            _context.PurchaseReturns.Add(ret);
            await _context.SaveChangesAsync();
            foreach (var l in parsed)
                _context.PurchaseReturnLines.Add(new PurchaseReturnLine { PurchaseReturnId = ret.Id, InventoryItemId = l.inventoryItemId, Quantity = l.quantity, UnitCost = l.unitCost });
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Create", "PurchaseReturn", ret.Id, $"Purchase return {ret.ReturnNumber}", branchId);
            return Json(new { success = true, id = ret.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var ret = await _context.PurchaseReturns.FirstOrDefaultAsync(r => r.Id == id);
            if (ret == null || !CanAccessBranch(ret.BranchId)) return Json(new { success = false, message = "Not found." });
            var (ok, msg) = await _supply.ApprovePurchaseReturnAsync(id, GetCurrentUserId() ?? 0, HttpContext.Session.GetUserName() ?? "System");
            if (ok) await _audit.LogAsync("Approve", "PurchaseReturn", id, "Approved", ret.BranchId);
            return Json(new { success = ok, message = msg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var ret = await _context.PurchaseReturns.FirstOrDefaultAsync(r => r.Id == id);
            if (ret == null || !CanAccessBranch(ret.BranchId) || ret.Status != "Pending") return Json(new { success = false });
            ret.Status = "Rejected"; ret.ApprovedById = GetCurrentUserId(); ret.ApprovedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private async Task<string> GenAsync(string prefix, IQueryable<string> existing)
        {
            var p = $"{prefix}-{DateTime.Now:yyyyMMdd}";
            var n = await existing.CountAsync(x => x.StartsWith(p));
            for (var s = n + 1; ; s++) { var c = $"{p}-{s:D3}"; if (!await existing.AnyAsync(x => x == c)) return c; }
        }
    }
}
