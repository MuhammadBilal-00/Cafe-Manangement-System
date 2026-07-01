using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 7: holiday calendar.</summary>
    [RequireManagerOrOwner]
    public class HolidayController : BaseController
    {
        public HolidayController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(await _context.Holidays.OrderBy(h => h.Date).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, DateTime date, int? branchId, bool isRecurring)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (id == 0) _context.Holidays.Add(new Holiday { Name = name.Trim(), Date = date.Date, BranchId = branchId, IsRecurring = isRecurring });
            else
            {
                var h = await _context.Holidays.FirstOrDefaultAsync(x => x.Id == id);
                if (h == null) return Json(new { success = false, message = "Not found." });
                h.Name = name.Trim(); h.Date = date.Date; h.BranchId = branchId; h.IsRecurring = isRecurring;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var h = await _context.Holidays.FindAsync(id);
            if (h == null) return Json(new { success = false });
            _context.Holidays.Remove(h);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
