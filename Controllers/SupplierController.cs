using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class SupplierController : BaseController
    {
        private readonly INotificationService _notificationService;

        public SupplierController(ApplicationDbContext context, INotificationService notificationService) : base(context)
        {
            _notificationService = notificationService;
        }

        // GET: Supplier
        public async Task<IActionResult> Index(int? branchId, bool? activeOnly)
        {
            var effectiveBranchId = GetEffectiveBranchId(branchId);

            var query = _context.Suppliers
                .Include(s => s.Branch)
                .AsQueryable();

            if (effectiveBranchId.HasValue)
                query = query.Where(s => s.BranchId == effectiveBranchId.Value);

            if (activeOnly == true)
                query = query.Where(s => s.IsActive);

            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();

            // Efficient aggregate counts — avoids N+1 loading
            var supplierIds = suppliers.Select(s => s.Id).ToList();
            var itemCounts = await _context.InventoryItems
                .Where(i => i.SupplierId.HasValue && supplierIds.Contains(i.SupplierId.Value))
                .GroupBy(i => i.SupplierId!.Value)
                .Select(g => new { SupplierId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Count);
            var purchaseCounts = await _context.Purchases
                .Where(p => p.SupplierId.HasValue && supplierIds.Contains(p.SupplierId.Value))
                .GroupBy(p => p.SupplierId!.Value)
                .Select(g => new { SupplierId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Count);

            ViewBag.ItemCounts = itemCounts;
            ViewBag.PurchaseCounts = purchaseCounts;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effectiveBranchId;
            ViewBag.ActiveOnly = activeOnly ?? true;

            return View(suppliers);
        }

        // GET: Supplier/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Branch)
                .Include(s => s.InventoryItems)
                .Include(s => s.Purchases)
                    .ThenInclude(p => p.Item)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null) return NotFound();
            if (!CanAccessBranch(supplier.BranchId)) return AccessDenied();

            return View(supplier);
        }

        // GET: Supplier/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(new Supplier());
        }

        // POST: Supplier/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (!CanAccessBranch(supplier.BranchId)) return AccessDenied();

            ModelState.Remove("Branch");
            ModelState.Remove("InventoryItems");
            ModelState.Remove("Purchases");

            if (ModelState.IsValid)
            {
                supplier.CreatedAt = DateTime.Now;
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationAsync(
                    "Supplier Added",
                    $"Supplier \"{supplier.Name}\" has been added.",
                    "Info", NotificationCategory.Inventory,
                    branchId: supplier.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Supplier/Index",
                    icon: "fas fa-truck");

                SetSuccessMessage($"Supplier \"{supplier.Name}\" created successfully.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(supplier);
        }

        // GET: Supplier/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers.Include(s => s.Branch).FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();
            if (!CanAccessBranch(supplier.BranchId)) return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            return View(supplier);
        }

        // POST: Supplier/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.Id) return NotFound();
            if (!CanAccessBranch(supplier.BranchId)) return AccessDenied();

            ModelState.Remove("Branch");
            ModelState.Remove("InventoryItems");
            ModelState.Remove("Purchases");

            if (ModelState.IsValid)
            {
                try
                {
                    supplier.UpdatedAt = DateTime.Now;
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                    SetSuccessMessage("Supplier updated successfully.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Suppliers.Any(s => s.Id == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(supplier);
        }

        // POST: Supplier/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            if (!CanAccessBranch(supplier.BranchId)) return AccessDenied();

            supplier.IsActive = !supplier.IsActive;
            supplier.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            SetSuccessMessage(supplier.IsActive ? "Supplier reactivated." : "Supplier deactivated.");
            return RedirectToAction(nameof(Index));
        }

        // GET: Supplier/Delete/5
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.Include(s => s.Branch).FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        // POST: Supplier/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                // Unlink items but don't delete them
                var items = await _context.InventoryItems.Where(i => i.SupplierId == id).ToListAsync();
                foreach (var item in items)
                    item.SupplierId = null;

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                SetSuccessMessage("Supplier deleted.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
