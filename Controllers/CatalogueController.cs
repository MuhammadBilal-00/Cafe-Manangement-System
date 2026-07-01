using Cafe.Data;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 6: public read-only digital menu for a branch (linked from a QR code). No login.</summary>
    public class CatalogueController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenant;

        public CatalogueController(ApplicationDbContext db, ITenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        [HttpGet("/Catalogue/Menu/{branchId:int}")]
        public async Task<IActionResult> Menu(int branchId)
        {
            // Public page: bypass tenant scoping and load this branch's live menu directly.
            using (_tenant.BypassFilter())
            {
                var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive);
                if (branch == null) return NotFound();

                var items = await _db.MenuItems.AsNoTracking().Include(m => m.Category)
                    .Where(m => m.BranchId == branchId && m.Availability)
                    .OrderBy(m => m.Category.Name).ThenBy(m => m.Name)
                    .ToListAsync();

                ViewBag.Branch = branch;
                return View(items);
            }
        }
    }
}
