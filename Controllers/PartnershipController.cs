using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    [RequireFeature("Marketing")]
    [RequireManagerOrOwner]
    public class PartnershipController : BaseController
    {
        private readonly INotificationService _notificationService;

        public PartnershipController(ApplicationDbContext context, INotificationService notificationService) : base(context)
        {
            _notificationService = notificationService;
        }

        private bool CanManage(int? branchId)
        {
            var role = GetCurrentUserRole();
            if (role == "Owner") return true;
            if (role == "BranchManager")
            {
                var managed = HttpContext.Session.GetManagedBranchId();
                return branchId.HasValue && managed.HasValue && branchId.Value == managed.Value;
            }
            return false;
        }

        // GET: Partnership
        public async Task<IActionResult> Index(int? branchId)
        {
            var effectiveBranchId = GetEffectiveBranchId(branchId);
            var query = _context.Partnerships.Include(p => p.Branch).AsQueryable();

            if (GetCurrentUserRole() == "BranchManager")
            {
                var managed = HttpContext.Session.GetManagedBranchId();
                query = query.Where(p => p.BranchId == managed || p.BranchId == null);
            }
            else if (effectiveBranchId.HasValue)
            {
                query = query.Where(p => p.BranchId == effectiveBranchId.Value || p.BranchId == null);
            }

            var partnerships = await query
                .OrderBy(p => p.PartnerName).ThenBy(p => p.CardTier)
                .ToListAsync();

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effectiveBranchId;
            ViewBag.Now = DateTime.Now;
            return View(partnerships);
        }

        // GET: Partnership/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(new Partnership
            {
                ValidFrom = DateTime.Today,
                ValidUntil = DateTime.Today.AddMonths(3)
            });
        }

        // POST: Partnership/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Partnership partnership)
        {
            ModelState.Remove("Branch");
            ModelState.Remove("CreatedBy");

            if (GetCurrentUserRole() == "BranchManager")
                partnership.BranchId = HttpContext.Session.GetManagedBranchId();

            if (partnership.BranchId.HasValue && !CanAccessBranch(partnership.BranchId.Value))
                ModelState.AddModelError(nameof(partnership.BranchId), "You cannot create a partnership for that branch.");

            if (partnership.ValidUntil < partnership.ValidFrom)
                ModelState.AddModelError(nameof(partnership.ValidUntil), "End date must be after the start date.");

            if (partnership.DiscountPercentage is < 0 or > 100)
                ModelState.AddModelError(nameof(partnership.DiscountPercentage), "Discount must be between 0 and 100.");

            if (ModelState.IsValid)
            {
                partnership.PartnerName = partnership.PartnerName.Trim();
                partnership.CardTier = partnership.CardTier.Trim();
                partnership.CreatedById = GetCurrentUserId();
                partnership.CreatedAt = DateTime.Now;
                _context.Partnerships.Add(partnership);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationAsync(
                    "Bank Partnership Added",
                    $"{partnership.DisplayName} — {partnership.DiscountPercentage:0.##}% off is now live.",
                    "Info", NotificationCategory.Financial,
                    branchId: partnership.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Partnership/Index",
                    icon: "fas fa-credit-card");

                SetSuccessMessage($"Partnership \"{partnership.DisplayName}\" created.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(partnership);
        }

        // GET: Partnership/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var partnership = await _context.Partnerships.FindAsync(id);
            if (partnership == null) return NotFound();
            if (!CanManage(partnership.BranchId)) return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            return View(partnership);
        }

        // POST: Partnership/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Partnership partnership)
        {
            if (id != partnership.Id) return NotFound();

            var existing = await _context.Partnerships.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            if (!CanManage(existing.BranchId)) return AccessDenied();

            ModelState.Remove("Branch");
            ModelState.Remove("CreatedBy");

            if (GetCurrentUserRole() == "BranchManager")
                partnership.BranchId = existing.BranchId;

            if (partnership.ValidUntil < partnership.ValidFrom)
                ModelState.AddModelError(nameof(partnership.ValidUntil), "End date must be after the start date.");

            if (partnership.DiscountPercentage is < 0 or > 100)
                ModelState.AddModelError(nameof(partnership.DiscountPercentage), "Discount must be between 0 and 100.");

            if (!CanManage(partnership.BranchId)) return AccessDenied();

            if (ModelState.IsValid)
            {
                existing.PartnerName = partnership.PartnerName.Trim();
                existing.CardTier = partnership.CardTier.Trim();
                existing.DiscountPercentage = partnership.DiscountPercentage;
                existing.MaxDiscountAmount = partnership.MaxDiscountAmount;
                existing.MinimumOrderAmount = partnership.MinimumOrderAmount;
                existing.ValidFrom = partnership.ValidFrom;
                existing.ValidUntil = partnership.ValidUntil;
                existing.IsActive = partnership.IsActive;
                existing.BranchId = partnership.BranchId;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                SetSuccessMessage($"Partnership \"{existing.DisplayName}\" updated.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(partnership);
        }

        // POST: Partnership/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var partnership = await _context.Partnerships.FindAsync(id);
            if (partnership == null) return NotFound();
            if (!CanManage(partnership.BranchId)) return AccessDenied();

            partnership.IsActive = !partnership.IsActive;
            partnership.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            SetSuccessMessage(partnership.IsActive ? "Partnership activated." : "Partnership deactivated.");
            return RedirectToAction(nameof(Index));
        }

        // GET: Partnership/Delete/5
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var partnership = await _context.Partnerships.Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == id);
            if (partnership == null) return NotFound();
            return View(partnership);
        }

        // POST: Partnership/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var partnership = await _context.Partnerships.FindAsync(id);
            if (partnership != null)
            {
                _context.Partnerships.Remove(partnership);
                await _context.SaveChangesAsync();
                SetSuccessMessage("Partnership deleted.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
