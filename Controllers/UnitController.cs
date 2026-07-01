using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class UnitController : BaseController
    {
        public UnitController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            ViewBag.Units = await _context.Units.OrderBy(u => u.Name).ToListAsync();
            return View(ViewBag.Units as List<Unit>);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, string abbreviation, int? baseUnitId, decimal conversionFactor = 1, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(abbreviation))
                return Json(new { success = false, message = "Name and abbreviation are required." });
            name = name.Trim();
            if (await _context.Units.AnyAsync(u => u.Name == name && u.Id != id))
                return Json(new { success = false, message = "A unit with that name already exists." });
            if (baseUnitId == id && id != 0) baseUnitId = null; // can't be its own base
            if (conversionFactor <= 0) conversionFactor = 1;

            if (id == 0)
                _context.Units.Add(new Unit { Name = name, Abbreviation = abbreviation.Trim(), BaseUnitId = baseUnitId, ConversionFactor = conversionFactor, IsActive = isActive });
            else
            {
                var u = await _context.Units.FirstOrDefaultAsync(x => x.Id == id);
                if (u == null) return Json(new { success = false, message = "Not found." });
                u.Name = name; u.Abbreviation = abbreviation.Trim(); u.BaseUnitId = baseUnitId; u.ConversionFactor = conversionFactor; u.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _context.Units.FindAsync(id);
            if (u == null) return Json(new { success = false });
            _context.Units.Remove(u);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
