using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Models;
using Cafe.Data;

namespace Cafe.Controllers
{
    public class InventoryItemController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryItemController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: InventoryItem
        public async Task<IActionResult> Index(int? branchId)
        {
            var branches = await _context.Branches.ToListAsync();
            ViewBag.Branches = branches;
            ViewBag.SelectedBranchId = branchId;

            IQueryable<InventoryItem> items = _context.InventoryItems
                .Include(i => i.Branch)
                .Include(i => i.Purchases);

            if (branchId.HasValue)
            {
                items = items.Where(i => i.BranchId == branchId.Value);
                var selectedBranch = await _context.Branches.FindAsync(branchId.Value);
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

            return View(inventoryItem);
        }

        // GET: InventoryItem/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await _context.Branches.ToListAsync();
            return View();
        }

        // POST: InventoryItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InventoryItem inventoryItem)
        {
            if (ModelState.IsValid)
            {
                inventoryItem.LastUpdated = DateTime.Now;
                _context.Add(inventoryItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Inventory item created successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await _context.Branches.ToListAsync();
            return View(inventoryItem);
        }

        // GET: InventoryItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem == null) return NotFound();

            ViewBag.Branches = await _context.Branches.ToListAsync();
            return View(inventoryItem);
        }

        // POST: InventoryItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InventoryItem inventoryItem)
        {
            if (id != inventoryItem.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    inventoryItem.LastUpdated = DateTime.Now;
                    _context.Update(inventoryItem);
                    await _context.SaveChangesAsync();
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

            ViewBag.Branches = await _context.Branches.ToListAsync();
            return View(inventoryItem);
        }

        // GET: InventoryItem/Delete/5
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem != null)
            {
                _context.InventoryItems.Remove(inventoryItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Inventory item deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: InventoryItem/Restock
        [HttpPost]
        public async Task<IActionResult> Restock(int id, decimal quantity)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem != null)
            {
                // Convert decimal input to int (round as you prefer; here we round)
                int add = (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
                inventoryItem.Quantity += add;

                inventoryItem.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // GET: InventoryItem/LowStock
        public async Task<IActionResult> LowStock()
        {
            var lowStockItems = await _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.Quantity <= i.ReorderLevel)
                .OrderBy(i => i.Quantity)
                .ToListAsync();

            return View(lowStockItems);
        }

        // GET: InventoryItem/OutOfStock
        public async Task<IActionResult> OutOfStock()
        {
            var outOfStockItems = await _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.Quantity == 0)
                .ToListAsync();

            return View(outOfStockItems);
        }

        // POST: InventoryItem/AdjustStock
        [HttpPost]
        public async Task<IActionResult> AdjustStock(int id, decimal newQuantity, string reason)
        {
            var inventoryItem = await _context.InventoryItems.FindAsync(id);
            if (inventoryItem != null)
            {
                int qty = (int)Math.Round(newQuantity, MidpointRounding.AwayFromZero);
                inventoryItem.Quantity = qty;

                inventoryItem.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // GET: Get inventory value report
        public async Task<IActionResult> ValueReport()
        {
            var items = await _context.InventoryItems
                .Include(i => i.Branch)
                .ToListAsync();

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
    }
}