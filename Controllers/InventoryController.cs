using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Services;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class InventoryController : BaseController
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(ApplicationDbContext context, IInventoryService inventoryService) 
            : base(context)
        {
            _inventoryService = inventoryService;
        }

        // Main Index Page - Dashboard
        public async Task<IActionResult> Index(int? branchId)
        {
            await PopulateViewBagData(branchId);
            
            var selectedBranchId = GetSelectedBranchId(branchId);
            if (!selectedBranchId.HasValue)
            {
                ViewBag.ErrorMessage = "Please select a branch to view inventory";
                return View(new InventoryDashboardViewModel());
            }

            var dashboard = await GetDashboardData(selectedBranchId.Value);
            return View(dashboard);
        }

        // Get Dashboard Data
        private async Task<InventoryDashboardViewModel> GetDashboardData(int branchId)
        {
            var items = await _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.BranchId == branchId)
                .ToListAsync();

            var dashboard = new InventoryDashboardViewModel
            {
                TotalItems = items.Count,
                LowStockItems = items.Count(i => i.Status == "Low Stock"),
                OutOfStockItems = items.Count(i => i.Status == "Out of Stock"),
                InStockItems = items.Count(i => i.Status == "In Stock"),
                TotalInventoryValue = items.Sum(i => i.CurrentQuantity * i.CostPerUnit),
                RecentlyUpdated = items
                    .OrderByDescending(i => i.LastUpdated)
                    .Take(5)
                    .Select(i => MapToViewModel(i))
                    .ToList(),
                LowStockAlerts = items
                    .Where(i => i.Status == "Low Stock" || i.Status == "Out of Stock")
                    .OrderBy(i => i.CurrentQuantity)
                    .Select(i => MapToViewModel(i))
                    .ToList()
            };

            return dashboard;
        }

        // Get Inventory Items as JSON
        [HttpGet]
        public async Task<IActionResult> GetInventoryItems(int? branchId, string? category, string? status, string? search, int page = 1, int pageSize = 10)
        {
            var selectedBranchId = GetSelectedBranchId(branchId);
            if (!selectedBranchId.HasValue)
            {
                return Json(new { success = false, message = "Branch not specified" });
            }

            var query = _context.InventoryItems
                .Include(i => i.Branch)
                .Where(i => i.BranchId == selectedBranchId.Value);

            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(i => i.Category == category);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i => i.Name.Contains(search) || 
                                       (i.Supplier != null && i.Supplier.Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(i => i.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new
                {
                    id = i.Id,
                    name = i.Name,
                    category = i.Category,
                    unit = i.Unit,
                    currentQuantity = i.CurrentQuantity,
                    minimumThreshold = i.MinimumThreshold,
                    costPerUnit = i.CostPerUnit,
                    supplier = i.Supplier,
                    status = i.Status,
                    lastUpdated = i.LastUpdated.ToString("yyyy-MM-dd HH:mm"),
                    branchName = i.Branch.Name,
                    totalValue = i.CurrentQuantity * i.CostPerUnit
                })
                .ToListAsync();

            return Json(new
            {
                items = items,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                currentPage = page
            });
        }

        // Create - GET
        [RequireManagerOrOwner]
        public async Task<IActionResult> Create(int? branchId)
        {
            await PopulateViewBagData(branchId);
            return View();
        }

        // Create - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Create(InventoryItem item)
        {
            try
            {
                if (!CanAccessBranch(item.BranchId))
                {
                    TempData["ErrorMessage"] = "Access denied to this branch";
                    return RedirectToAction(nameof(Index));
                }

                if (ModelState.IsValid)
                {
                    item.LastUpdated = DateTime.Now;
                    item.Status = await _inventoryService.GetInventoryStatus(item.CurrentQuantity, item.MinimumThreshold);

                    _context.InventoryItems.Add(item);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Inventory item '{item.Name}' created successfully";
                    return RedirectToAction(nameof(Index), new { branchId = item.BranchId });
                }

                await PopulateViewBagData(item.BranchId);
                return View(item);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating inventory item: {ex.Message}";
                await PopulateViewBagData(item.BranchId);
                return View(item);
            }
        }

        // Edit - GET
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null || !CanAccessBranch(item.BranchId))
            {
                TempData["ErrorMessage"] = "Inventory item not found or access denied";
                return RedirectToAction(nameof(Index));
            }

            await PopulateViewBagData(item.BranchId);
            return View(item);
        }

        // Edit - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id, InventoryItem item)
        {
            if (id != item.Id)
            {
                TempData["ErrorMessage"] = "Invalid inventory item";
                return RedirectToAction(nameof(Index));
            }

            if (!CanAccessBranch(item.BranchId))
            {
                TempData["ErrorMessage"] = "Access denied to this branch";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    item.LastUpdated = DateTime.Now;
                    item.Status = await _inventoryService.GetInventoryStatus(item.CurrentQuantity, item.MinimumThreshold);

                    _context.Update(item);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Inventory item '{item.Name}' updated successfully";
                    return RedirectToAction(nameof(Index), new { branchId = item.BranchId });
                }

                await PopulateViewBagData(item.BranchId);
                return View(item);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating inventory item: {ex.Message}";
                await PopulateViewBagData(item.BranchId);
                return View(item);
            }
        }

        // Details
        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.InventoryItems
                .Include(i => i.Branch)
                .Include(i => i.Transactions.OrderByDescending(t => t.TransactionDate).Take(10))
                .Include(i => i.RecipeMappings)
                    .ThenInclude(rm => rm.MenuItem)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null || !CanAccessBranch(item.BranchId))
            {
                TempData["ErrorMessage"] = "Inventory item not found or access denied";
                return RedirectToAction(nameof(Index));
            }

            await PopulateViewBagData(item.BranchId);
            return View(item);
        }

        // Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(id);
                if (item == null || !CanAccessBranch(item.BranchId))
                {
                    return Json(new { success = false, message = "Inventory item not found or access denied" });
                }

                var branchId = item.BranchId;
                _context.InventoryItems.Remove(item);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Inventory item deleted successfully", branchId = branchId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting inventory item: {ex.Message}" });
            }
        }

        // Stock In - GET
        public async Task<IActionResult> StockIn(int? branchId)
        {
            await PopulateViewBagData(branchId);
            var selectedBranchId = GetSelectedBranchId(branchId);
            
            if (selectedBranchId.HasValue)
            {
                ViewBag.InventoryItems = await _context.InventoryItems
                    .Where(i => i.BranchId == selectedBranchId.Value)
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }

            return View();
        }

        // Stock In - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInViewModel model)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(model.InventoryItemId);
                if (item == null || !CanAccessBranch(item.BranchId))
                {
                    TempData["ErrorMessage"] = "Inventory item not found or access denied";
                    return RedirectToAction(nameof(StockIn));
                }

                var userName = HttpContext.Session.GetUserName() ?? "System";
                var success = await _inventoryService.StockIn(
                    model.InventoryItemId,
                    model.Quantity,
                    model.Notes,
                    userName
                );

                if (success)
                {
                    TempData["SuccessMessage"] = $"Stock added successfully. New quantity: {item.CurrentQuantity + model.Quantity}";
                    return RedirectToAction(nameof(Index), new { branchId = item.BranchId });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to add stock";
                    return RedirectToAction(nameof(StockIn), new { branchId = item.BranchId });
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error adding stock: {ex.Message}";
                return RedirectToAction(nameof(StockIn));
            }
        }

        // Stock Out - GET
        public async Task<IActionResult> StockOut(int? branchId)
        {
            await PopulateViewBagData(branchId);
            var selectedBranchId = GetSelectedBranchId(branchId);
            
            if (selectedBranchId.HasValue)
            {
                ViewBag.InventoryItems = await _context.InventoryItems
                    .Where(i => i.BranchId == selectedBranchId.Value)
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }

            return View();
        }

        // Stock Out - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockOutViewModel model)
        {
            try
            {
                var item = await _context.InventoryItems.FindAsync(model.InventoryItemId);
                if (item == null || !CanAccessBranch(item.BranchId))
                {
                    TempData["ErrorMessage"] = "Inventory item not found or access denied";
                    return RedirectToAction(nameof(StockOut));
                }

                if (item.CurrentQuantity < model.Quantity)
                {
                    TempData["ErrorMessage"] = $"Insufficient stock. Current quantity: {item.CurrentQuantity} {item.Unit}";
                    return RedirectToAction(nameof(StockOut), new { branchId = item.BranchId });
                }

                var userName = HttpContext.Session.GetUserName() ?? "System";
                var success = await _inventoryService.StockOut(
                    model.InventoryItemId,
                    model.Quantity,
                    model.TransactionType,
                    model.Notes,
                    userName
                );

                if (success)
                {
                    TempData["SuccessMessage"] = $"Stock removed successfully. New quantity: {item.CurrentQuantity - model.Quantity}";
                    return RedirectToAction(nameof(Index), new { branchId = item.BranchId });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove stock";
                    return RedirectToAction(nameof(StockOut), new { branchId = item.BranchId });
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error removing stock: {ex.Message}";
                return RedirectToAction(nameof(StockOut));
            }
        }

        // Transactions History
        public async Task<IActionResult> Transactions(int? branchId, int? inventoryItemId, string? transactionType, int page = 1)
        {
            await PopulateViewBagData(branchId);
            var selectedBranchId = GetSelectedBranchId(branchId);

            if (!selectedBranchId.HasValue)
            {
                ViewBag.ErrorMessage = "Please select a branch to view transactions";
                return View(new List<InventoryTransactionViewModel>());
            }

            var query = _context.InventoryTransactions
                .Include(t => t.InventoryItem)
                .Include(t => t.Branch)
                .Where(t => t.BranchId == selectedBranchId.Value);

            if (inventoryItemId.HasValue)
            {
                query = query.Where(t => t.InventoryItemId == inventoryItemId.Value);
            }

            if (!string.IsNullOrEmpty(transactionType))
            {
                query = query.Where(t => t.TransactionType == transactionType);
            }

            ViewBag.InventoryItems = await _context.InventoryItems
                .Where(i => i.BranchId == selectedBranchId.Value)
                .OrderBy(i => i.Name)
                .ToListAsync();

            var pageSize = 20;
            var totalCount = await query.CountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new InventoryTransactionViewModel
                {
                    Id = t.Id,
                    InventoryItemName = t.InventoryItem.Name,
                    TransactionType = t.TransactionType,
                    Quantity = t.Quantity,
                    QuantityBefore = t.QuantityBefore,
                    QuantityAfter = t.QuantityAfter,
                    Notes = t.Notes,
                    TransactionDate = t.TransactionDate,
                    PerformedBy = t.PerformedBy,
                    BranchName = t.Branch.Name
                })
                .ToListAsync();

            return View(transactions);
        }

        // Recipe Mappings
        [RequireManagerOrOwner]
        public async Task<IActionResult> RecipeMappings(int? branchId, int? menuItemId)
        {
            await PopulateViewBagData(branchId);
            var selectedBranchId = GetSelectedBranchId(branchId);

            if (!selectedBranchId.HasValue)
            {
                ViewBag.ErrorMessage = "Please select a branch";
                return View(new List<RecipeMappingViewModel>());
            }

            var query = _context.InventoryRecipeMappings
                .Include(rm => rm.MenuItem)
                .Include(rm => rm.InventoryItem)
                .Where(rm => rm.InventoryItem.BranchId == selectedBranchId.Value);

            if (menuItemId.HasValue)
            {
                query = query.Where(rm => rm.MenuItemId == menuItemId.Value);
            }

            ViewBag.MenuItems = await _context.MenuItems
                .Where(m => m.BranchId == selectedBranchId.Value)
                .OrderBy(m => m.Name)
                .ToListAsync();

            ViewBag.InventoryItems = await _context.InventoryItems
                .Where(i => i.BranchId == selectedBranchId.Value)
                .OrderBy(i => i.Name)
                .ToListAsync();

            var mappings = await query
                .Select(rm => new RecipeMappingViewModel
                {
                    Id = rm.Id,
                    MenuItemId = rm.MenuItemId,
                    MenuItemName = rm.MenuItem.Name,
                    InventoryItemId = rm.InventoryItemId,
                    InventoryItemName = rm.InventoryItem.Name,
                    QuantityRequired = rm.QuantityRequired,
                    Unit = rm.Unit
                })
                .ToListAsync();

            return View(mappings);
        }

        // Add Recipe Mapping - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> AddRecipeMapping(int menuItemId, int inventoryItemId, decimal quantityRequired, string unit)
        {
            try
            {
                var menuItem = await _context.MenuItems.FindAsync(menuItemId);
                var inventoryItem = await _context.InventoryItems.FindAsync(inventoryItemId);

                if (menuItem == null || inventoryItem == null || !CanAccessBranch(menuItem.BranchId))
                {
                    return Json(new { success = false, message = "Invalid menu item or inventory item" });
                }

                var existingMapping = await _context.InventoryRecipeMappings
                    .FirstOrDefaultAsync(rm => rm.MenuItemId == menuItemId && rm.InventoryItemId == inventoryItemId);

                if (existingMapping != null)
                {
                    return Json(new { success = false, message = "This mapping already exists" });
                }

                var mapping = new InventoryRecipeMapping
                {
                    MenuItemId = menuItemId,
                    InventoryItemId = inventoryItemId,
                    QuantityRequired = quantityRequired,
                    Unit = inventoryItem.Unit // Auto-populate from inventory item
                };

                _context.InventoryRecipeMappings.Add(mapping);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Recipe mapping added successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Delete Recipe Mapping
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> DeleteRecipeMapping(int id)
        {
            try
            {
                var mapping = await _context.InventoryRecipeMappings
                    .Include(rm => rm.MenuItem)
                    .FirstOrDefaultAsync(rm => rm.Id == id);

                if (mapping == null || !CanAccessBranch(mapping.MenuItem.BranchId))
                {
                    return Json(new { success = false, message = "Mapping not found or access denied" });
                }

                _context.InventoryRecipeMappings.Remove(mapping);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Recipe mapping deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Helper Methods
        private InventoryItemViewModel MapToViewModel(InventoryItem item)
        {
            return new InventoryItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                Unit = item.Unit,
                CurrentQuantity = item.CurrentQuantity,
                MinimumThreshold = item.MinimumThreshold,
                CostPerUnit = item.CostPerUnit,
                Supplier = item.Supplier,
                LastUpdated = item.LastUpdated,
                Status = item.Status,
                BranchId = item.BranchId,
                BranchName = item.Branch?.Name ?? ""
            };
        }

        private int? GetSelectedBranchId(int? branchId)
        {
            if (HttpContext.Session.IsOwner() && branchId.HasValue)
                return branchId.Value;
            
            if (HttpContext.Session.IsBranchManager())
                return HttpContext.Session.GetManagedBranchId();
            
            if (HttpContext.Session.IsStaff())
                return HttpContext.Session.GetStaffBranchId();

            return branchId;
        }

        private async Task<List<Branch>> GetAccessibleBranches()
        {
            var branchesQuery = _context.Branches.AsQueryable();

            if (HttpContext.Session.IsBranchManager())
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                {
                    branchesQuery = branchesQuery.Where(b => b.Id == managedBranchId.Value);
                }
            }
            else if (HttpContext.Session.IsStaff())
            {
                var staffBranchId = HttpContext.Session.GetStaffBranchId();
                if (staffBranchId.HasValue)
                {
                    branchesQuery = branchesQuery.Where(b => b.Id == staffBranchId.Value);
                }
            }

            return await branchesQuery.Where(b => b.IsActive).ToListAsync();
        }

        private async Task PopulateViewBagData(int? branchId)
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.UserRole = HttpContext.Session.GetUserRole();
            ViewBag.UserName = HttpContext.Session.GetUserName();
            ViewBag.IsOwner = HttpContext.Session.IsOwner();
            ViewBag.IsBranchManager = HttpContext.Session.IsBranchManager();
            ViewBag.IsStaff = HttpContext.Session.IsStaff();
            ViewBag.SelectedBranchId = GetSelectedBranchId(branchId);
        }
    }
}
