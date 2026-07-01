using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 9 (60): configurable POS &amp; receipt profiles.</summary>
    [RequireManagerOrOwner]
    public class PosProfileController : BaseController
    {
        public PosProfileController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(await _context.PosProfiles.OrderBy(p => p.Name).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, int? branchId, string paperSize, bool showLogo, bool showTaxBreakdown, string? headerText, string? footerText, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (paperSize is not ("A5" or "Thermal80")) paperSize = "A5";
            if (isDefault) // only one default
                foreach (var p in await _context.PosProfiles.Where(p => p.IsDefault).ToListAsync()) p.IsDefault = false;

            if (id == 0) _context.PosProfiles.Add(new PosProfile { Name = name.Trim(), BranchId = branchId, PaperSize = paperSize, ShowLogo = showLogo, ShowTaxBreakdown = showTaxBreakdown, HeaderText = headerText, FooterText = footerText, IsDefault = isDefault });
            else
            {
                var p = await _context.PosProfiles.FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Json(new { success = false });
                p.Name = name.Trim(); p.BranchId = branchId; p.PaperSize = paperSize; p.ShowLogo = showLogo; p.ShowTaxBreakdown = showTaxBreakdown; p.HeaderText = headerText; p.FooterText = footerText; p.IsDefault = isDefault;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.PosProfiles.FindAsync(id);
            if (p == null) return Json(new { success = false });
            _context.PosProfiles.Remove(p); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
