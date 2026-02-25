using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cafe.Data;
using Cafe.Models;
using Cafe.Attributes;
using Cafe.Helpers;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class MenuItemController : BaseController
    {
        public MenuItemController(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IActionResult> Index(int? branchId, int? categoryId, string search, string dietaryFilter, bool showInactive = false)
        {
            var menuItemsQuery = _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.Branch)
                .Include(m => m.Reviews)
                .AsQueryable();

            // Apply branch filtering based on user role
            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

                if (userBranchId.HasValue)
                {
                    menuItemsQuery = menuItemsQuery.Where(m => m.BranchId == userBranchId.Value);
                }
            }
            else if (branchId.HasValue)
            {
                menuItemsQuery = menuItemsQuery.Where(m => m.BranchId == branchId.Value);
            }

            // Apply filters
            if (categoryId.HasValue)
            {
                menuItemsQuery = menuItemsQuery.Where(m => m.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                menuItemsQuery = menuItemsQuery.Where(m =>
                    m.Name.Contains(search) ||
                    (m.Description != null && m.Description.Contains(search)) ||
                    (m.Tags != null && m.Tags.Contains(search)));
            }

            if (!string.IsNullOrEmpty(dietaryFilter))
            {
                switch (dietaryFilter.ToLower())
                {
                    case "vegetarian":
                        menuItemsQuery = menuItemsQuery.Where(m => m.IsVegetarian);
                        break;
                    case "vegan":
                        menuItemsQuery = menuItemsQuery.Where(m => m.IsVegan);
                        break;
                    case "glutenfree":
                        menuItemsQuery = menuItemsQuery.Where(m => m.IsGlutenFree);
                        break;
                    case "dairyfree":
                        menuItemsQuery = menuItemsQuery.Where(m => m.IsDairyFree);
                        break;
                    case "spicy":
                        menuItemsQuery = menuItemsQuery.Where(m => m.IsSpicy);
                        break;
                }
            }

            if (!showInactive)
            {
                menuItemsQuery = menuItemsQuery.Where(m => m.Availability);
            }

            // Populate ViewBag data
            await PopulateDropdowns();

            ViewBag.SelectedBranch = branchId;
            ViewBag.SelectedCategory = categoryId;
            ViewBag.Search = search;
            ViewBag.DietaryFilter = dietaryFilter;
            ViewBag.ShowInactive = showInactive;

            return View(await menuItemsQuery.OrderBy(m => m.Category.Name).ThenBy(m => m.DisplayOrder).ThenBy(m => m.Name).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var menuItem = await _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.Branch)
                .Include(m => m.Ingredients)
                .ThenInclude(mi => mi.Ingredient)
                .Include(m => m.Reviews.OrderByDescending(r => r.ReviewDate))
                .ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            return View(menuItem);
        }

        [RequireManagerOrOwner]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();

            // Pre-populate branch for non-owners
            var model = new MenuItem();
            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

                if (userBranchId.HasValue)
                {
                    model.BranchId = userBranchId.Value;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Create(MenuItem menuItem)
        {
            // Enforce branch for non-owners
            if (!HttpContext.Session.IsOwner())
            {
                var branchId = HttpContext.Session.GetManagedBranchId() 
                            ?? HttpContext.Session.GetStaffBranchId();
                if (branchId.HasValue) 
                {
                    menuItem.BranchId = branchId.Value;
                }
            }
            
            // Check access
            if (!CanAccessBranch(menuItem.BranchId))
            {
                TempData["Error"] = "Access denied to this branch.";
                await PopulateDropdowns();
                return View(menuItem);
            }

            // Remove navigation property validation errors (they are not bound from form)
            ModelState.Remove("Category");
            ModelState.Remove("Branch");
            ModelState.Remove("OrderItems");
            ModelState.Remove("Ingredients");
            ModelState.Remove("Reviews");
            
            // Validate
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please check all required fields.";
                await PopulateDropdowns();
                return View(menuItem);
            }
            
            try
            {
                // Set timestamps
                menuItem.CreatedDate = DateTime.Now;
                menuItem.LastUpdated = DateTime.Now;

                // Set defaults if not provided
                if (menuItem.DisplayOrder == 0)
                {
                    var maxOrder = await _context.MenuItems
                        .Where(m => m.CategoryId == menuItem.CategoryId && m.BranchId == menuItem.BranchId)
                        .MaxAsync(m => (int?)m.DisplayOrder) ?? 0;
                    menuItem.DisplayOrder = maxOrder + 1;
                }

                _context.Add(menuItem);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Menu item created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating menu item: {ex.Message}";
                await PopulateDropdowns();
                return View(menuItem);
            }
        }
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            await PopulateDropdowns();
            return View(menuItem); // Fixed: return the view, not redirect
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,LongDescription,Price,OriginalPrice,CostPrice,Availability,CategoryId,BranchId,CreatedDate,ImageUrl,ImageGallery,Calories,Protein,Carbohydrates,Fat,Fiber,Sugar,Sodium,IsVegetarian,IsVegan,IsGlutenFree,IsDairyFree,IsNutFree,IsSpicy,SpiceLevel,PreparationTime,IsFeatured,IsSpecial,DisplayOrder,Tags")] MenuItem menuItem)
        {
            if (id != menuItem.Id) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            // Remove navigation property validation errors
            ModelState.Remove("Category");
            ModelState.Remove("Branch");
            ModelState.Remove("OrderItems");
            ModelState.Remove("Ingredients");
            ModelState.Remove("Reviews");

            if (ModelState.IsValid)
            {
                try
                {
                    menuItem.LastUpdated = DateTime.Now;
                    _context.Update(menuItem);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Menu item updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuItemExists(menuItem.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns();
    return View(menuItem); // <-- keep returning View so errors show properly
        }

        [RequireManagerOrOwner]
        public async Task<IActionResult> ManageIngredients(int? id)
        {
            if (id == null) return NotFound();

            var menuItem = await _context.MenuItems
                .Include(m => m.Ingredients)
                .ThenInclude(mi => mi.Ingredient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            ViewBag.AvailableIngredients = await _context.Ingredients
                .Where(i => i.IsActive)
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = i.Name + " (" + i.Unit + ")"
                })
                .ToListAsync();

            return View(menuItem); // Fixed: return the view, not redirect
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> AddIngredient(int menuItemId, int ingredientId, decimal quantity, string unit, bool isOptional, decimal? extraCharge)
        {
            var menuItem = await _context.MenuItems.FindAsync(menuItemId);
            if (menuItem == null) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            // Check if ingredient already exists for this menu item
            var existingIngredient = await _context.MenuItemIngredients
                .FirstOrDefaultAsync(mi => mi.MenuItemId == menuItemId && mi.IngredientId == ingredientId);

            if (existingIngredient != null)
            {
                TempData["Error"] = "This ingredient is already added to the menu item.";
                return RedirectToAction("ManageIngredients", new { id = menuItemId });
            }

            var menuItemIngredient = new MenuItemIngredient
            {
                MenuItemId = menuItemId,
                IngredientId = ingredientId,
                Quantity = quantity,
                Unit = unit,
                IsOptional = isOptional,
                ExtraCharge = extraCharge
            };

            _context.MenuItemIngredients.Add(menuItemIngredient);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Ingredient added successfully!";
            return RedirectToAction("ManageIngredients", new { id = menuItemId });
        }

        // Bulk operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> BulkUpdateAvailability(int[] itemIds, bool availability)
        {
            var items = await _context.MenuItems
                .Where(m => itemIds.Contains(m.Id))
                .ToListAsync();

            foreach (var item in items)
            {
                if (CanAccessBranch(item.BranchId))
                {
                    item.Availability = availability;
                    item.LastUpdated = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Updated availability for {items.Count} items.";
            return RedirectToAction(nameof(Index));
        }

        // Helper Methods
        private async Task PopulateDropdowns()
        {
            // Fix for Branches - convert to SelectListItem
            var accessibleBranches = await GetAccessibleBranches();
            ViewBag.Branches = accessibleBranches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToList();

            // Fix for Categories - convert to SelectListItem  
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();

            ViewBag.SpiceLevels = new List<SelectListItem>
    {
        new SelectListItem { Value = "1", Text = "Mild" },
        new SelectListItem { Value = "2", Text = "Medium" },
        new SelectListItem { Value = "3", Text = "Hot" },
        new SelectListItem { Value = "4", Text = "Very Hot" },
        new SelectListItem { Value = "5", Text = "Extremely Hot" }
    };
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }
        [RequireManagerOrOwner]
        public async Task<IActionResult> RemoveIngredient(int menuItemId, int ingredientId)
        {
            var menuItemIngredient = await _context.MenuItemIngredients
                .FirstOrDefaultAsync(mi => mi.MenuItemId == menuItemId && mi.IngredientId == ingredientId);

            if (menuItemIngredient != null)
            {
                _context.MenuItemIngredients.Remove(menuItemIngredient);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Ingredient removed successfully!";
            }
            else
            {
                TempData["Error"] = "Ingredient not found!";
            }

            return RedirectToAction("ManageIngredients", new { id = menuItemId });
        }

        [RequireManagerOrOwner]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var menuItem = await _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null) return NotFound();

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            return View(menuItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            
            if (menuItem == null)
            {
                TempData["Error"] = "Menu item not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!CanAccessBranch(menuItem.BranchId))
            {
                return AccessDenied();
            }

            // Check if menu item is used in any orders
            var hasOrders = await _context.OrderItems
                .AnyAsync(oi => oi.MenuItemId == id);

            if (hasOrders)
            {
                // Soft delete - just mark as unavailable
                menuItem.Availability = false;
                menuItem.LastUpdated = DateTime.Now;
                _context.Update(menuItem);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Menu item marked as unavailable (has order history).";
            }
            else
            {
                // Hard delete
                _context.MenuItems.Remove(menuItem);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Menu item deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ExportCsv()
        {
            var query = _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.Branch)
                .AsQueryable();

            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();
                if (userBranchId.HasValue)
                    query = query.Where(m => m.BranchId == userBranchId.Value);
            }

            var items = await query.OrderBy(m => m.Name).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Name,Description,Price,CostPrice,Category,Branch,Availability,Calories,PreparationTime(min),AverageRating,Tags,CreatedDate");
            foreach (var m in items)
            {
                csv.AppendLine($"{EscapeCsv(m.Name)},{EscapeCsv(m.Description ?? "")},{m.Price:F2},{m.CostPrice:F2},{EscapeCsv(m.Category?.Name ?? "")},{EscapeCsv(m.Branch?.Name ?? "")},{m.Availability},{m.Calories?.ToString() ?? ""},{m.PreparationTime},{m.AverageRating?.ToString("F1") ?? ""},{EscapeCsv(m.Tags ?? "")},{m.CreatedDate:yyyy-MM-dd}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"menu-items-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"\"")}\""; 
            return value;
        }
    }
}
