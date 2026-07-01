using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 7: sales targets &amp; commissions — target vs actual sales for the period.</summary>
    [RequireFeature("Analytics")]
    [RequireManagerOrOwner]
    public class SalesTargetController : BaseController
    {
        public SalesTargetController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            var targets = await _context.SalesTargets.Include(t => t.Staff).ThenInclude(s => s!.User)
                .OrderByDescending(t => t.Year).ThenByDescending(t => t.Month).Take(100).ToListAsync();

            // Actual sales per target (staff via ServiceStaffId, else branch), same period.
            var achievement = new Dictionary<int, decimal>();
            foreach (var t in targets)
            {
                var from = new DateTime(t.Year, t.Month, 1);
                var to = from.AddMonths(1);
                var q = _context.Orders.Where(o => o.OrderDate >= from && o.OrderDate < to && o.Status != "Cancelled");
                if (t.StaffId.HasValue) q = q.Where(o => o.ServiceStaffId == t.StaffId.Value);
                else if (t.BranchId.HasValue) q = q.Where(o => o.BranchId == t.BranchId.Value);
                achievement[t.Id] = await q.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            }

            ViewBag.Achievement = achievement;
            ViewBag.Staff = await GetAccessibleStaff();
            ViewBag.Branches = await GetAccessibleBranches();
            return View(targets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int? staffId, int? branchId, int year, int month, decimal targetAmount, decimal commissionPercent)
        {
            if (month < 1 || month > 12) return Json(new { success = false, message = "Invalid month." });
            if (id == 0) _context.SalesTargets.Add(new SalesTarget { StaffId = staffId, BranchId = branchId, Year = year, Month = month, TargetAmount = targetAmount, CommissionPercent = commissionPercent });
            else
            {
                var t = await _context.SalesTargets.FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return Json(new { success = false });
                t.StaffId = staffId; t.BranchId = branchId; t.Year = year; t.Month = month; t.TargetAmount = targetAmount; t.CommissionPercent = commissionPercent;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var t = await _context.SalesTargets.FindAsync(id);
            if (t == null) return Json(new { success = false });
            _context.SalesTargets.Remove(t); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
