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
    public class ProductionController : BaseController
    {
        private readonly ISupplyChainService _supply;
        private readonly IAuditLogService _audit;

        public ProductionController(ApplicationDbContext context, ISupplyChainService supply, IAuditLogService audit) : base(context)
        {
            _supply = supply;
            _audit = audit;
        }

        private record Input(int inventoryItemId, decimal quantity);

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var orders = await _context.ProductionOrders
                .Include(p => p.Branch).Include(p => p.OutputItem).Include(p => p.Inputs)
                .Where(p => branchIds.Contains(p.BranchId))
                .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync();
            ViewBag.Branches = await GetAccessibleBranches();
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var items = await _context.InventoryItems.Where(i => i.BranchId == branchId)
                .OrderBy(i => i.Name).Select(i => new { id = i.Id, name = i.Name, unit = i.Unit, qty = i.Quantity, cost = i.UnitPrice }).ToListAsync();
            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int branchId, int outputInventoryItemId, decimal outputQuantity, string? notes, string inputs)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (outputQuantity <= 0) return Json(new { success = false, message = "Output quantity must be positive." });

            List<Input> parsed;
            try { parsed = JsonSerializer.Deserialize<List<Input>>(inputs ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid inputs." }); }
            parsed = parsed.Where(i => i.inventoryItemId > 0 && i.quantity > 0).ToList();
            if (parsed.Count == 0) return Json(new { success = false, message = "Add at least one input." });
            if (parsed.Any(i => i.inventoryItemId == outputInventoryItemId))
                return Json(new { success = false, message = "The output item can't also be an input." });

            var order = new ProductionOrder
            {
                BranchId = branchId, OutputInventoryItemId = outputInventoryItemId, OutputQuantity = outputQuantity,
                Status = "Draft", Notes = notes, CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            };
            _context.ProductionOrders.Add(order);
            await _context.SaveChangesAsync();
            foreach (var i in parsed)
                _context.ProductionInputs.Add(new ProductionInput { ProductionOrderId = order.Id, InventoryItemId = i.inventoryItemId, Quantity = i.quantity });
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Create", "ProductionOrder", order.Id, "Draft production order", branchId);
            return Json(new { success = true, id = order.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var order = await _context.ProductionOrders.FirstOrDefaultAsync(p => p.Id == id);
            if (order == null || !CanAccessBranch(order.BranchId)) return Json(new { success = false, message = "Not found." });
            var (ok, msg) = await _supply.CompleteProductionAsync(id, HttpContext.Session.GetUserName() ?? "System");
            if (ok) await _audit.LogAsync("Complete", "ProductionOrder", id, msg, order.BranchId);
            return Json(new { success = ok, message = msg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _context.ProductionOrders.FirstOrDefaultAsync(p => p.Id == id);
            if (order == null || !CanAccessBranch(order.BranchId)) return Json(new { success = false });
            if (order.Status != "Draft") return Json(new { success = false, message = "Only drafts can be cancelled." });
            order.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
