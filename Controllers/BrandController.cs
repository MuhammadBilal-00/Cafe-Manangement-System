using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class BrandController : BaseController
    {
        public BrandController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index() =>
            View(await _context.Brands.OrderBy(b => b.Name).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, string? description, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            name = name.Trim();
            if (await _context.Brands.AnyAsync(b => b.Name == name && b.Id != id))
                return Json(new { success = false, message = "A brand with that name already exists." });

            if (id == 0)
                _context.Brands.Add(new Brand { Name = name, Description = description, IsActive = isActive });
            else
            {
                var b = await _context.Brands.FirstOrDefaultAsync(x => x.Id == id);
                if (b == null) return Json(new { success = false, message = "Not found." });
                b.Name = name; b.Description = description; b.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var b = await _context.Brands.FindAsync(id);
            if (b == null) return Json(new { success = false });
            _context.Brands.Remove(b);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
