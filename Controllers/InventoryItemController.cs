using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // GET: InventoryItem/Index
        public async Task<IActionResult> Index(int? branchId)
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Branches = branches;
            ViewBag.SelectedBranchId = branchId;

            var items = _context.InventoryItems
                .Include(i => i.Branch)
                .AsQueryable();

            if (branchId.HasValue)
            {
                items = items.Where(i => i.BranchId == branchId.Value);
                ViewBag.CurrentBranch = branches.FirstOrDefault(b => b.Id == branchId.Value)?.Name ?? "All Branches";
            }
            else
            {
                ViewBag.CurrentBranch = "All Branches";
            }

            return View(await items.OrderBy(i => i.CurrentQuantity).ToListAsync());
        }

        // GET: InventoryItem/Create
        public async Task<IActionResult> Create()
        {
            await PopulateBranchesDropdown();
            return View(new InventoryItem { LastUpdated = DateTime.Now });
        }

        // POST: InventoryItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,CurrentQuantity,Unit,BranchId,MinimumThreshold,CostPerUnit")] InventoryItem inventoryItem)
        {
            ModelState.Remove("Branch");
            ModelState.Remove("Purchases");
            ModelState.Remove("Category"); // Will be set manually

            if (ModelState.IsValid)
            {
                inventoryItem.LastUpdated = DateTime.Now;
                // Set default Category if not bound
                if (string.IsNullOrEmpty(inventoryItem.Category))
                {
                    inventoryItem.Category = "General";
                }
                _context.Add(inventoryItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"✅ '{inventoryItem.Name}' added to inventory!";
                return RedirectToAction(nameof(Index));
            }

            // Shows exactly which fields failed — helpful during development
            var errors = ModelState
                .Where(x => x.Value!.Errors.Any())
                .Select(x => $"{x.Key}: {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}");
            TempData["Error"] = "Validation failed: " + string.Join(" | ", errors);

            await PopulateBranchesDropdown(inventoryItem.BranchId);
            return View(inventoryItem);
        }

        // GET: InventoryItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound();

            await PopulateBranchesDropdown(item.BranchId);
            return View(item);
        }

        // POST: InventoryItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,CurrentQuantity,Unit,BranchId,MinimumThreshold,CostPerUnit")] InventoryItem inventoryItem)
        {
            if (id != inventoryItem.Id) return NotFound();

            ModelState.Remove("Branch");
            ModelState.Remove("Purchases");
            ModelState.Remove("Category"); // Will be preserved from existing

            if (ModelState.IsValid)
            {
                try
                {
                    inventoryItem.LastUpdated = DateTime.Now;
                    // Preserve Category from existing item if not bound
                    var existingItem = await _context.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
                    if (existingItem != null && string.IsNullOrEmpty(inventoryItem.Category))
                    {
                        inventoryItem.Category = existingItem.Category;
                    }
                    _context.Update(inventoryItem);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"✅ '{inventoryItem.Name}' updated!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InventoryItemExists(inventoryItem.Id)) return NotFound();
                    throw;
                }
            }

            await PopulateBranchesDropdown(inventoryItem.BranchId);
            return View(inventoryItem);
        }

        // GET: InventoryItem/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InventoryItems
                .Include(i => i.Branch)
                .Include(i => i.Purchases)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();
            return View(item);
        }

        // GET: InventoryItem/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InventoryItems
                .Include(i => i.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();
            return View(item);
        }

        // POST: InventoryItem/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item != null)
            {
                var name = item.Name;
                _context.InventoryItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"🗑️ '{name}' removed from inventory.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: InventoryItem/Restock (AJAX)
        [HttpPost]
        public async Task<IActionResult> Restock(int id, int quantity)
        {
            if (quantity <= 0)
                return Json(new { success = false, message = "Quantity must be greater than 0" });

            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
                return Json(new { success = false, message = "Item not found" });

            item.CurrentQuantity += quantity;
            item.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, newQuantity = item.CurrentQuantity });
        }

        // POST: InventoryItem/AdjustStock (AJAX)
        [HttpPost]
        public async Task<IActionResult> AdjustStock(int id, int newQuantity)
        {
            if (newQuantity < 0)
                return Json(new { success = false, message = "Quantity cannot be negative" });

            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
                return Json(new { success = false });

            item.CurrentQuantity = newQuantity;
            item.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, newQuantity = item.CurrentQuantity });
        }

        private async Task PopulateBranchesDropdown(int? selectedId = null)
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Branches = branches;
            ViewBag.BranchSelectList = new SelectList(branches, "Id", "Name", selectedId);
        }

        private bool InventoryItemExists(int id) =>
            _context.InventoryItems.Any(e => e.Id == id);
    }
}