using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class PriceGroupController : BaseController
    {
        public PriceGroupController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index() =>
            View(await _context.PriceGroups.OrderBy(p => p.Name).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, string? description, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            name = name.Trim();
            if (await _context.PriceGroups.AnyAsync(p => p.Name == name && p.Id != id))
                return Json(new { success = false, message = "That price group already exists." });

            if (id == 0)
                _context.PriceGroups.Add(new PriceGroup { Name = name, Description = description, IsActive = isActive });
            else
            {
                var p = await _context.PriceGroups.FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Json(new { success = false, message = "Not found." });
                p.Name = name; p.Description = description; p.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.PriceGroups.FindAsync(id);
            if (p == null) return Json(new { success = false });
            _context.PriceGroups.Remove(p);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
