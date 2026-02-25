using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Models;
using Cafe.Data;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Services;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class InventoryItemController : BaseController
    {
        private readonly IAuditLogService _auditLogService;

        public InventoryItemController(ApplicationDbContext context, IAuditLogService auditLogService) : base(context)
        {
            _auditLogService = auditLogService;
        }

        // Helper: get the branch this user is scoped to (null = all branches for Owner)
        private int? GetEffectiveBranchId(int? requestedBranchId)
        {
            var role = GetCurrentUserRole();
            if (role == "BranchManager")
                return HttpContext.Session.GetManagedBranchId();
            return requestedBranchId; // Owner can pick any
        }

        // Helper: verify the item belongs to an accessible branch
        private bool CanAccessItem(InventoryItem item)
        {
            return CanAccessBranch(item.BranchId);
        }

        // Helper: get branches the user can see
        private async Task<System.Collections.Generic.List<Branch>> GetAccessibleBranches()
        {
            var role = GetCurrentUserRole();
            if (role == "Owner")
                return await _context.Branches.Where(b => b.IsActive).ToListAsync();

            var branchId = HttpContext.Session.GetManagedBranchId();
            if (branchId.HasValue)
                return await _context.Branches.Where(b => b.Id == branchId.Value && b.IsActive).ToListAsync();

            return new System.Collections.Generic.List<Branch>();
        }

        // GET: InventoryItem
        public async Task<IActionResult> Index(int? branchId)
        {
            var branches = await GetAccessibleBranches();
            ViewBag.Branches = branches;

            var effectiveBranchId = GetEffectiveBranchId(branchId);
            ViewBag.SelectedBranchId = effectiveBranchId;

            IQueryable<InventoryItem> items = _context.InventoryItems
                .Include(i => i.Branch)
                .Include(i => i.Purchases);

            if (effectiveBranchId.HasValue)
            {
                items = items.Where(i => i.BranchId == effectiveBranchId.Value);
                var selectedBranch = await _context.Branches.FindAsync(effectiveBranchId.Value);
                ViewBag.CurrentBranch = selectedBranch?.Name ?? "All Branches";
            }
            else
            {
                ViewBag.CurrentBranch = "All Branches";
            }

            return View(await items.OrderBy(i => i.Quantity).ToListAsync());
        }

        // GET: InventoryItem/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var inventoryItem = await _context.InventoryItems
                .Include(i => i.Branch)
                .Include(i => i.Purchases)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (inventoryItem == null) return NotFound();
            if (!CanAccessItem(inventoryItem)) return AccessDenied();

            return View(inventoryItem);
        }

        // GET: InventoryItem/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View();
        }

        // POST: InventoryItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InventoryItem inventoryItem)
        {
            // Manager can only create items in their own branch
            if (!CanAccessBranch(inventoryItem.BranchId))
                return AccessDenied();

            // Remove navigation property validation errors (not bound from form)
            ModelState.Remove("Branch");
            ModelState.Remove("Purchases");

            if (ModelState.IsValid)
            {
                inventoryItem.LastUpdated = DateTime.Now;
                _context.Add(inventoryItem);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("Create", "InventoryItem", inventoryItem.Id,
                    $"Created inventory item: {inventoryItem.Name} (Qty: {inventoryItem.Quantity}, Branch: {inventoryItem.BranchId})",
                    inventoryItem.BranchId);

                TempData["Success"] = "Inventory item created successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(inventoryItem);
        }

        // GET: InventoryItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem == null) return NotFound();
            if (!CanAccessItem(inventoryItem)) return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            return View(inventoryItem);
        }

        // POST: InventoryItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InventoryItem inventoryItem)
        {
            if (id != inventoryItem.Id) return NotFound();
            if (!CanAccessBranch(inventoryItem.BranchId)) return AccessDenied();

            // Remove navigation property validation errors (not bound from form)
            ModelState.Remove("Branch");
            ModelState.Remove("Purchases");

            if (ModelState.IsValid)
            {
                try
                {
                    inventoryItem.LastUpdated = DateTime.Now;
                    _context.Update(inventoryItem);
                    await _context.SaveChangesAsync();

                    await _auditLogService.LogAsync("Update", "InventoryItem", inventoryItem.Id,
                        $"Updated inventory item: {inventoryItem.Name} (Qty: {inventoryItem.Quantity}, Branch: {inventoryItem.BranchId})",
                        inventoryItem.BranchId);

                    TempData["Success"] = "Inventory item updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InventoryItemExists(inventoryItem.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(inventoryItem);
        }

        // GET: InventoryItem/Delete/5
        [RequireOwner]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var inventoryItem = await _context.InventoryItems
                .Include(i => i.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (inventoryItem == null) return NotFound();

            return View(inventoryItem);
        }

        // POST: InventoryItem/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem != null)
            {
                var itemName = inventoryItem.Name;
                var itemBranchId = inventoryItem.BranchId;
                _context.InventoryItems.Remove(inventoryItem);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("Delete", "InventoryItem", id,
                    $"Deleted inventory item: {itemName}",
                    itemBranchId);

                TempData["Success"] = "Inventory item deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: InventoryItem/Restock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(int id, decimal quantity)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem == null)
                return Json(new { success = false });
            if (!CanAccessItem(inventoryItem))
                return Json(new { success = false, message = "Access denied" });

            int add = (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
            inventoryItem.Quantity += add;
            inventoryItem.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("Restock", "InventoryItem", inventoryItem.Id,
                $"Restocked inventory item: {inventoryItem.Name} (+{add}, New Qty: {inventoryItem.Quantity})",
                inventoryItem.BranchId);

            return Json(new { success = true });
        }

        // GET: InventoryItem/LowStock
        public async Task<IActionResult> LowStock()
        {
            var query = _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.Quantity <= i.ReorderLevel)
                .AsQueryable();

            var effectiveBranchId = GetEffectiveBranchId(null);
            if (effectiveBranchId.HasValue)
                query = query.Where(i => i.BranchId == effectiveBranchId.Value);

            var lowStockItems = await query.OrderBy(i => i.Quantity).ToListAsync();
            return View(lowStockItems);
        }

        // GET: InventoryItem/OutOfStock
        public async Task<IActionResult> OutOfStock()
        {
            var query = _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.Quantity == 0)
                .AsQueryable();

            var effectiveBranchId = GetEffectiveBranchId(null);
            if (effectiveBranchId.HasValue)
                query = query.Where(i => i.BranchId == effectiveBranchId.Value);

            var outOfStockItems = await query.ToListAsync();
            return View(outOfStockItems);
        }

        // POST: InventoryItem/AdjustStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(int id, decimal newQuantity, string reason)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem == null)
                return Json(new { success = false });
            if (!CanAccessItem(inventoryItem))
                return Json(new { success = false, message = "Access denied" });

            int qty = (int)Math.Round(newQuantity, MidpointRounding.AwayFromZero);
            inventoryItem.Quantity = qty;
            inventoryItem.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // GET: Get inventory value report
        public async Task<IActionResult> ValueReport()
        {
            var query = _context.InventoryItems
                .Include(i => i.Branch)
                .AsQueryable();

            var effectiveBranchId = GetEffectiveBranchId(null);
            if (effectiveBranchId.HasValue)
                query = query.Where(i => i.BranchId == effectiveBranchId.Value);

            var items = await query.ToListAsync();

            var report = items.GroupBy(i => i.Branch)
                .Select(g => new
                {
                    BranchName = g.Key.Name,
                    TotalItems = g.Count(),
                    TotalValue = g.Sum(i => i.Quantity * i.UnitPrice),
                    LowStockItems = g.Count(i => i.Quantity <= i.ReorderLevel)
                })
                .ToList();

            return View(report);
        }

        private bool InventoryItemExists(int id)
        {
            return _context.InventoryItems.Any(e => e.Id == id);
        }

        public async Task<IActionResult> ExportCsv(int? branchId)
        {
            var effectiveBranchId = GetEffectiveBranchId(branchId);

            IQueryable<InventoryItem> query = _context.InventoryItems.Include(i => i.Branch);

            if (effectiveBranchId.HasValue)
                query = query.Where(i => i.BranchId == effectiveBranchId.Value);

            var items = await query.OrderBy(i => i.Name).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Name,Quantity,Unit,Branch,ReorderLevel,UnitPrice,LastUpdated");
            foreach (var i in items)
            {
                csv.AppendLine($"{EscapeCsv(i.Name)},{i.Quantity},{EscapeCsv(i.Unit)},{EscapeCsv(i.Branch?.Name ?? "")},{i.ReorderLevel},{i.UnitPrice:F2},{i.LastUpdated:yyyy-MM-dd}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"inventory-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"\"")}\""; 
            return value;
        }
    }
}