using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 4: tax groups with (possibly compound) taxes.</summary>
    [RequireManagerOrOwner]
    public class TaxGroupController : BaseController
    {
        public TaxGroupController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index() =>
            View(await _context.TaxGroups.Include(g => g.Taxes).OrderBy(g => g.Name).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGroup(int id, string name, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            name = name.Trim();
            if (await _context.TaxGroups.AnyAsync(g => g.Name == name && g.Id != id))
                return Json(new { success = false, message = "That tax group already exists." });
            if (id == 0) _context.TaxGroups.Add(new TaxGroup { Name = name, IsActive = isActive });
            else
            {
                var g = await _context.TaxGroups.FirstOrDefaultAsync(x => x.Id == id);
                if (g == null) return Json(new { success = false, message = "Not found." });
                g.Name = name; g.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            var g = await _context.TaxGroups.FindAsync(id);
            if (g == null) return Json(new { success = false });
            _context.TaxGroups.Remove(g); // taxes cascade
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTax(int id, int taxGroupId, string name, decimal rate, bool isCompound, int sortOrder)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (!await _context.TaxGroups.AnyAsync(g => g.Id == taxGroupId)) return Json(new { success = false, message = "Group not found." });
            if (id == 0) _context.Taxes.Add(new Tax { TaxGroupId = taxGroupId, Name = name.Trim(), Rate = rate, IsCompound = isCompound, SortOrder = sortOrder });
            else
            {
                var t = await _context.Taxes.FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return Json(new { success = false, message = "Not found." });
                t.Name = name.Trim(); t.Rate = rate; t.IsCompound = isCompound; t.SortOrder = sortOrder;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTax(int id)
        {
            var t = await _context.Taxes.FindAsync(id);
            if (t == null) return Json(new { success = false });
            _context.Taxes.Remove(t);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
