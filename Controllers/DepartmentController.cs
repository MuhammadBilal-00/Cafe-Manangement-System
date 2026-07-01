using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 7: departments &amp; designations (org structure).</summary>
    [RequireManagerOrOwner]
    public class DepartmentController : BaseController
    {
        public DepartmentController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            ViewBag.Designations = await _context.Designations.OrderBy(d => d.Name).ToListAsync();
            return View(await _context.Departments.OrderBy(d => d.Name).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDepartment(int id, string name, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            name = name.Trim();
            if (await _context.Departments.AnyAsync(d => d.Name == name && d.Id != id)) return Json(new { success = false, message = "Already exists." });
            if (id == 0) _context.Departments.Add(new Department { Name = name, IsActive = isActive });
            else { var d = await _context.Departments.FirstOrDefaultAsync(x => x.Id == id); if (d == null) return Json(new { success = false }); d.Name = name; d.IsActive = isActive; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDesignation(int id, string name, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            name = name.Trim();
            if (await _context.Designations.AnyAsync(d => d.Name == name && d.Id != id)) return Json(new { success = false, message = "Already exists." });
            if (id == 0) _context.Designations.Add(new Designation { Name = name, IsActive = isActive });
            else { var d = await _context.Designations.FirstOrDefaultAsync(x => x.Id == id); if (d == null) return Json(new { success = false }); d.Name = name; d.IsActive = isActive; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var d = await _context.Departments.FindAsync(id);
            if (d == null) return Json(new { success = false });
            _context.Departments.Remove(d); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteDesignation(int id)
        {
            var d = await _context.Designations.FindAsync(id);
            if (d == null) return Json(new { success = false });
            _context.Designations.Remove(d); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
