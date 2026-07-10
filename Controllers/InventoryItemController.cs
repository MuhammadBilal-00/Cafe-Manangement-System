using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Models;
using Cafe.Data;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Services;

namespace Cafe.Controllers
{
    [RequireFeature("Inventory")]
    [RequireManagerOrOwner]
    public class InventoryItemController : BaseController
    {
        private readonly INotificationService _notificationService;
        private readonly IInventoryService _inventoryService;

        public InventoryItemController(ApplicationDbContext context, INotificationService notificationService, IInventoryService inventoryService) : base(context)
        {
            _notificationService = notificationService;
            _inventoryService = inventoryService;
        }

        // Helper: verify the item belongs to an accessible branch
        private bool CanAccessItem(InventoryItem item)
        {
            return CanAccessBranch(item.BranchId);
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
            ViewBag.Suppliers = await GetAccessibleSuppliers();
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
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                inventoryItem.LastUpdated = DateTime.Now;
                _context.Add(inventoryItem);
                await _context.SaveChangesAsync();

                // Notification: inventory item created
                await _notificationService.CreateNotificationAsync(
                    "Inventory Item Added",
                    $"\"{inventoryItem.Name}\" has been added to inventory.",
                    "Info", NotificationCategory.Inventory,
                    branchId: inventoryItem.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/InventoryItem/Index",
                    icon: "fas fa-boxes-stacked");

                // Check initial stock level
                if (inventoryItem.Quantity >= 0)
                {
                    if (inventoryItem.MinimumStock > 0 && inventoryItem.Quantity < inventoryItem.MinimumStock)
                    {
                        await _notificationService.CreateNotificationAsync(
                            "Critical Stock Level",
                            $"\"{inventoryItem.Name}\" was added with critically low stock: {inventoryItem.Quantity} units (minimum: {inventoryItem.MinimumStock}).",
                            "Error", NotificationCategory.Inventory,
                            branchId: inventoryItem.BranchId,
                            createdBy: GetCurrentUserId(),
                            redirectUrl: "/InventoryItem/LowStock",
                            icon: "fas fa-triangle-exclamation");
                    }
                    else if (inventoryItem.Quantity <= inventoryItem.ReorderLevel)
                    {
                        await _notificationService.CreateNotificationAsync(
                            "Low Stock Alert",
                            $"\"{inventoryItem.Name}\" was added with low stock: {inventoryItem.Quantity} units (reorder level: {inventoryItem.ReorderLevel}).",
                            "Warning", NotificationCategory.Inventory,
                            branchId: inventoryItem.BranchId,
                            createdBy: GetCurrentUserId(),
                            redirectUrl: "/InventoryItem/LowStock",
                            icon: "fas fa-triangle-exclamation");
                    }
                }

                TempData["Success"] = "Inventory item created successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.Suppliers = await GetAccessibleSuppliers();
            return View(inventoryItem);
        }

        // GET: InventoryItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var inventoryItem = await _context.InventoryItems.Include(i => i.Supplier).FirstOrDefaultAsync(i => i.Id == id);
            if (inventoryItem == null) return NotFound();
            if (!CanAccessItem(inventoryItem)) return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            // Include the existing supplier even if inactive, so the dropdown preserves the link.
            ViewBag.Suppliers = await GetAccessibleSuppliers(inventoryItem.SupplierId);
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
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                try
                {
                    inventoryItem.LastUpdated = DateTime.Now;
                    _context.Update(inventoryItem);
                    await _context.SaveChangesAsync();

                    // Check stock level after edit
                    if (inventoryItem.Quantity >= 0)
                    {
                        if (inventoryItem.MinimumStock > 0 && inventoryItem.Quantity < inventoryItem.MinimumStock)
                        {
                            await _notificationService.CreateNotificationAsync(
                                "Critical Stock Level",
                                $"\"{inventoryItem.Name}\" is critically low at {inventoryItem.Quantity} units (minimum: {inventoryItem.MinimumStock}).",
                                "Error", NotificationCategory.Inventory,
                                branchId: inventoryItem.BranchId,
                                createdBy: GetCurrentUserId(),
                                redirectUrl: "/InventoryItem/LowStock",
                                icon: "fas fa-triangle-exclamation");
                        }
                        else if (inventoryItem.Quantity <= inventoryItem.ReorderLevel)
                        {
                            await _notificationService.CreateNotificationAsync(
                                "Low Stock Alert",
                                $"\"{inventoryItem.Name}\" is at {inventoryItem.Quantity} units (reorder level: {inventoryItem.ReorderLevel}).",
                                "Warning", NotificationCategory.Inventory,
                                branchId: inventoryItem.BranchId,
                                createdBy: GetCurrentUserId(),
                                redirectUrl: "/InventoryItem/LowStock",
                                icon: "fas fa-triangle-exclamation");
                        }
                    }

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
            ViewBag.Suppliers = await GetAccessibleSuppliers();
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
                // An item with movement history or references is part of the audit trail —
                // deleting it would orphan the stock ledger, purchases and recipes. Block it;
                // dead items are simply zeroed out and stop appearing in reorder reports.
                var hasHistory = await _context.InventoryTransactions.AnyAsync(t => t.InventoryItemId == id)
                    || await _context.Purchases.AnyAsync(p => p.ItemId == id)
                    || await _context.InventoryRecipeMappings.AnyAsync(r => r.InventoryItemId == id);
                if (hasHistory)
                {
                    TempData["Error"] = "This item has stock history, purchases or recipe links and cannot be deleted. " +
                        "Adjust its quantity to 0 and remove its recipe links instead.";
                    return RedirectToAction(nameof(Index));
                }

                var itemName = inventoryItem.Name;
                var itemBranch = inventoryItem.BranchId;
                _context.InventoryItems.Remove(inventoryItem);
                await _context.SaveChangesAsync();

                // Notification: inventory deleted
                await _notificationService.CreateNotificationAsync(
                    "Inventory Item Deleted",
                    $"\"{itemName}\" has been removed from inventory.",
                    "Warning", NotificationCategory.Inventory,
                    branchId: itemBranch,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/InventoryItem/Index",
                    icon: "fas fa-trash");

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

            if (quantity <= 0)
                return Json(new { success = false, message = "Restock quantity must be greater than zero." });

            var performedBy = HttpContext.Session.GetUserName() ?? "System";
            var restocked = await _inventoryService.StockIn(id, quantity, "Restock", performedBy);
            if (!restocked)
                return Json(new { success = false, message = "Restock failed." });

            // The service updates the row via direct SQL, so refresh this tracked
            // instance before reading Quantity for the low-stock checks below.
            await _context.Entry(inventoryItem).ReloadAsync();

            if (inventoryItem.MinimumStock > 0 && inventoryItem.Quantity < inventoryItem.MinimumStock)
            {
                await _notificationService.CreateNotificationAsync(
                    "Critical Stock Level",
                    $"\"{inventoryItem.Name}\" is critically low at {inventoryItem.Quantity} units (minimum: {inventoryItem.MinimumStock}).",
                    "Error", NotificationCategory.Inventory,
                    branchId: inventoryItem.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/InventoryItem/LowStock",
                    icon: "fas fa-triangle-exclamation");
            }
            else if (inventoryItem.Quantity <= inventoryItem.ReorderLevel)
            {
                await _notificationService.CreateNotificationAsync(
                    "Low Stock Alert",
                    $"\"{inventoryItem.Name}\" is at {inventoryItem.Quantity} units (reorder level: {inventoryItem.ReorderLevel}).",
                    "Warning", NotificationCategory.Inventory,
                    branchId: inventoryItem.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/InventoryItem/LowStock",
                    icon: "fas fa-triangle-exclamation");
            }

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

            if (newQuantity < 0)
                return Json(new { success = false, message = "Quantity cannot be negative." });

            var performedBy = HttpContext.Session.GetUserName() ?? "System";
            var adjusted = await _inventoryService.AdjustStockAsync(id, newQuantity, reason, performedBy);
            if (!adjusted)
                return Json(new { success = false, message = "Stock adjustment failed." });

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