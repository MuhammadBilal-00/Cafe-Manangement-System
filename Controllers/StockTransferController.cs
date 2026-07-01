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
    public class StockTransferController : BaseController
    {
        private readonly ISupplyChainService _supply;
        private readonly IAuditLogService _audit;

        public StockTransferController(ApplicationDbContext context, ISupplyChainService supply, IAuditLogService audit) : base(context)
        {
            _supply = supply;
            _audit = audit;
        }

        private record Line(int inventoryItemId, decimal quantity);

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var transfers = await _context.StockTransfers
                .Include(t => t.FromBranch).Include(t => t.ToBranch).Include(t => t.Items)
                .Where(t => branchIds.Contains(t.FromBranchId) || branchIds.Contains(t.ToBranchId))
                .OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync();
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.AllBranches = await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            return View(transfers);
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
        public async Task<IActionResult> Create(int fromBranchId, int toBranchId, string? reference, string? notes, string items)
        {
            if (!CanAccessBranch(fromBranchId)) return Json(new { success = false, message = "You can only transfer from a branch you manage." });
            if (fromBranchId == toBranchId) return Json(new { success = false, message = "Choose two different branches." });

            List<Line> lines;
            try { lines = JsonSerializer.Deserialize<List<Line>>(items ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid items." }); }
            lines = lines.Where(l => l.inventoryItemId > 0 && l.quantity > 0).ToList();
            if (lines.Count == 0) return Json(new { success = false, message = "Add at least one item." });

            var transfer = new StockTransfer
            {
                FromBranchId = fromBranchId, ToBranchId = toBranchId, Status = "Draft",
                Reference = reference, Notes = notes, CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            };
            _context.StockTransfers.Add(transfer);
            await _context.SaveChangesAsync();
            foreach (var l in lines)
                _context.StockTransferItems.Add(new StockTransferItem { StockTransferId = transfer.Id, InventoryItemId = l.inventoryItemId, Quantity = l.quantity });
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Create", "StockTransfer", transfer.Id, $"Draft transfer {fromBranchId}→{toBranchId}", fromBranchId);
            return Json(new { success = true, id = transfer.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var transfer = await _context.StockTransfers.FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || !CanAccessBranch(transfer.FromBranchId)) return Json(new { success = false, message = "Not found." });
            var (ok, msg) = await _supply.CompleteTransferAsync(id, HttpContext.Session.GetUserName() ?? "System");
            if (ok) await _audit.LogAsync("Complete", "StockTransfer", id, "Transfer completed", transfer.FromBranchId);
            return Json(new { success = ok, message = msg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var transfer = await _context.StockTransfers.FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || !CanAccessBranch(transfer.FromBranchId)) return Json(new { success = false });
            if (transfer.Status != "Draft") return Json(new { success = false, message = "Only drafts can be cancelled." });
            transfer.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
