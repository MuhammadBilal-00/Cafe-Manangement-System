using System.Text.Json;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 2: combo / deal-meal builder. Combos expand to component menu items at the register.</summary>
    [RequireManagerOrOwner]
    public class ComboController : BaseController
    {
        public ComboController(ApplicationDbContext context) : base(context) { }

        private record ComboLine(int menuItemId, int quantity);

        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;

            var combos = effective.HasValue
                ? await _context.Combos.Include(c => c.Items).ThenInclude(i => i.MenuItem)
                    .Where(c => c.BranchId == effective.Value).OrderBy(c => c.Name).ToListAsync()
                : new List<Combo>();
            return View(combos);
        }

        [HttpGet]
        public async Task<IActionResult> GetMenuItems(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var items = await _context.MenuItems.Where(m => m.BranchId == branchId && m.Availability)
                .OrderBy(m => m.Name).Select(m => new { id = m.Id, name = m.Name, price = m.Price }).ToListAsync();
            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int branchId, string name, string? description, decimal price, string components)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (string.IsNullOrWhiteSpace(name) || price <= 0)
                return Json(new { success = false, message = "Name and a positive price are required." });

            List<ComboLine> lines;
            try { lines = JsonSerializer.Deserialize<List<ComboLine>>(components ?? "[]") ?? new(); }
            catch { return Json(new { success = false, message = "Invalid components." }); }
            lines = lines.Where(l => l.menuItemId > 0 && l.quantity > 0).ToList();
            if (lines.Count == 0) return Json(new { success = false, message = "Add at least one component." });

            Combo combo;
            if (id == 0)
            {
                combo = new Combo { BranchId = branchId };
                _context.Combos.Add(combo);
            }
            else
            {
                combo = await _context.Combos.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);
                if (combo == null || !CanAccessBranch(combo.BranchId)) return Json(new { success = false, message = "Not found." });
                _context.ComboItems.RemoveRange(combo.Items);
            }
            combo.Name = name.Trim();
            combo.Description = description;
            combo.Price = Math.Round(price, 2);
            await _context.SaveChangesAsync();

            foreach (var l in lines)
                _context.ComboItems.Add(new ComboItem { ComboId = combo.Id, MenuItemId = l.menuItemId, Quantity = l.quantity });
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var combo = await _context.Combos.FindAsync(id);
            if (combo == null || !CanAccessBranch(combo.BranchId)) return Json(new { success = false });
            _context.Combos.Remove(combo); // items cascade
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
