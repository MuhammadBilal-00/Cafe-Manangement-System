using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 2: modifier groups + options, and which menu items they attach to.</summary>
    [RequireManagerOrOwner]
    public class ModifierController : BaseController
    {
        public ModifierController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            var groups = await _context.ModifierGroups
                .Include(g => g.Modifiers)
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
                .ToListAsync();
            return View(groups);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGroup(int id, string name, int minSelect, int maxSelect, bool isRequired, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (maxSelect < 1) maxSelect = 1;
            if (minSelect < 0) minSelect = 0;

            if (id == 0)
                _context.ModifierGroups.Add(new ModifierGroup { Name = name.Trim(), MinSelect = minSelect, MaxSelect = maxSelect, IsRequired = isRequired, IsActive = isActive });
            else
            {
                var g = await _context.ModifierGroups.FirstOrDefaultAsync(x => x.Id == id);
                if (g == null) return Json(new { success = false, message = "Not found." });
                g.Name = name.Trim(); g.MinSelect = minSelect; g.MaxSelect = maxSelect; g.IsRequired = isRequired; g.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            if (await _context.MenuItemModifierGroups.AnyAsync(x => x.ModifierGroupId == id))
                return Json(new { success = false, message = "Detach this group from menu items first." });
            var g = await _context.ModifierGroups.FindAsync(id);
            if (g == null) return Json(new { success = false });
            _context.ModifierGroups.Remove(g); // options cascade
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOption(int id, int groupId, string name, decimal priceDelta, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (!await _context.ModifierGroups.AnyAsync(g => g.Id == groupId))
                return Json(new { success = false, message = "Group not found." });

            if (id == 0)
                _context.Modifiers.Add(new Modifier { ModifierGroupId = groupId, Name = name.Trim(), PriceDelta = priceDelta, IsActive = isActive });
            else
            {
                var o = await _context.Modifiers.FirstOrDefaultAsync(x => x.Id == id);
                if (o == null) return Json(new { success = false, message = "Not found." });
                o.Name = name.Trim(); o.PriceDelta = priceDelta; o.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOption(int id)
        {
            var o = await _context.Modifiers.FindAsync(id);
            if (o == null) return Json(new { success = false });
            _context.Modifiers.Remove(o);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // ── attach a group to menu items ──
        [HttpGet]
        public async Task<IActionResult> MenuItems()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var items = await _context.MenuItems.Include(m => m.Branch)
                .Where(m => branchIds.Contains(m.BranchId))
                .OrderBy(m => m.Name)
                .Select(m => new { id = m.Id, name = m.Name, branch = m.Branch.Name })
                .ToListAsync();
            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> Assignments(int groupId)
        {
            var ids = await _context.MenuItemModifierGroups
                .Where(x => x.ModifierGroupId == groupId).Select(x => x.MenuItemId).ToListAsync();
            return Json(ids);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAssignments(int groupId, [FromForm] List<int> menuItemIds)
        {
            menuItemIds ??= new List<int>();
            var existing = await _context.MenuItemModifierGroups.Where(x => x.ModifierGroupId == groupId).ToListAsync();
            _context.MenuItemModifierGroups.RemoveRange(existing.Where(e => !menuItemIds.Contains(e.MenuItemId)));
            var have = existing.Select(e => e.MenuItemId).ToHashSet();
            foreach (var mid in menuItemIds.Where(m => !have.Contains(m)))
                _context.MenuItemModifierGroups.Add(new MenuItemModifierGroup { MenuItemId = mid, ModifierGroupId = groupId });
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
