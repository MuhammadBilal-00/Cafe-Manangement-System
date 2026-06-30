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
    [RequireManagerOrOwner]
    public class PromoCodeController : BaseController
    {
        private readonly INotificationService _notificationService;

        public PromoCodeController(ApplicationDbContext context, INotificationService notificationService) : base(context)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Whether the current user may edit/delete an entry scoped to the given branch.
        /// Owner: anything (incl. global/null). Manager: only their own branch (never global).
        /// </summary>
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

        // GET: PromoCode
        public async Task<IActionResult> Index(int? branchId)
        {
            var effectiveBranchId = GetEffectiveBranchId(branchId);
            var query = _context.PromoCodes.Include(p => p.Branch).AsQueryable();

            if (GetCurrentUserRole() == "BranchManager")
            {
                // Managers see their branch's codes plus any global (all-branch) codes.
                var managed = HttpContext.Session.GetManagedBranchId();
                query = query.Where(p => p.BranchId == managed || p.BranchId == null);
            }
            else if (effectiveBranchId.HasValue)
            {
                query = query.Where(p => p.BranchId == effectiveBranchId.Value || p.BranchId == null);
            }

            var promos = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effectiveBranchId;
            return View(promos);
        }

        // GET: PromoCode/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(new PromoCode
            {
                ValidFrom = DateTime.Today,
                ValidUntil = DateTime.Today.AddMonths(1)
            });
        }

        // POST: PromoCode/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromoCode promo)
        {
            ModelState.Remove("Branch");
            ModelState.Remove("CreatedBy");

            // Managers can only create codes for their own branch.
            if (GetCurrentUserRole() == "BranchManager")
                promo.BranchId = HttpContext.Session.GetManagedBranchId();

            if (promo.BranchId.HasValue && !CanAccessBranch(promo.BranchId.Value))
            {
                ModelState.AddModelError(nameof(promo.BranchId), "You cannot create a code for that branch.");
            }

            promo.Code = (promo.Code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(promo.Code))
                ModelState.AddModelError(nameof(promo.Code), "Code is required.");
            else if (await _context.PromoCodes.AnyAsync(p => p.Code == promo.Code))
                ModelState.AddModelError(nameof(promo.Code), "That code already exists.");

            if (promo.ValidUntil < promo.ValidFrom)
                ModelState.AddModelError(nameof(promo.ValidUntil), "End date must be after the start date.");

            if (promo.DiscountType == "Percentage" && promo.DiscountValue > 100)
                ModelState.AddModelError(nameof(promo.DiscountValue), "Percentage cannot exceed 100.");

            if (ModelState.IsValid)
            {
                promo.CreatedById = GetCurrentUserId();
                promo.CreatedAt = DateTime.Now;
                promo.TimesUsed = 0;
                _context.PromoCodes.Add(promo);
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationAsync(
                    "Promo Code Created",
                    $"Promo \"{promo.Code}\" is now available.",
                    "Info", NotificationCategory.Financial,
                    branchId: promo.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/PromoCode/Index",
                    icon: "fas fa-tags");

                SetSuccessMessage($"Promo code \"{promo.Code}\" created.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(promo);
        }

        // GET: PromoCode/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null) return NotFound();
            if (!CanManage(promo.BranchId)) return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            return View(promo);
        }

        // POST: PromoCode/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PromoCode promo)
        {
            if (id != promo.Id) return NotFound();

            var existing = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            if (!CanManage(existing.BranchId)) return AccessDenied();

            ModelState.Remove("Branch");
            ModelState.Remove("CreatedBy");

            if (GetCurrentUserRole() == "BranchManager")
                promo.BranchId = existing.BranchId; // managers can't move a code to another branch

            promo.Code = (promo.Code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(promo.Code))
                ModelState.AddModelError(nameof(promo.Code), "Code is required.");
            else if (await _context.PromoCodes.AnyAsync(p => p.Code == promo.Code && p.Id != id))
                ModelState.AddModelError(nameof(promo.Code), "That code already exists.");

            if (promo.ValidUntil < promo.ValidFrom)
                ModelState.AddModelError(nameof(promo.ValidUntil), "End date must be after the start date.");

            if (!CanManage(promo.BranchId)) return AccessDenied();

            if (ModelState.IsValid)
            {
                existing.Code = promo.Code;
                existing.Description = promo.Description;
                existing.DiscountType = promo.DiscountType;
                existing.DiscountValue = promo.DiscountValue;
                existing.MinimumOrderAmount = promo.MinimumOrderAmount;
                existing.MaxDiscountAmount = promo.MaxDiscountAmount;
                existing.ValidFrom = promo.ValidFrom;
                existing.ValidUntil = promo.ValidUntil;
                existing.UsageLimit = promo.UsageLimit;
                existing.IsActive = promo.IsActive;
                existing.BranchId = promo.BranchId;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                SetSuccessMessage($"Promo code \"{existing.Code}\" updated.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(promo);
        }

        // POST: PromoCode/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null) return NotFound();
            if (!CanManage(promo.BranchId)) return AccessDenied();

            promo.IsActive = !promo.IsActive;
            promo.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            SetSuccessMessage(promo.IsActive ? "Promo code activated." : "Promo code deactivated.");
            return RedirectToAction(nameof(Index));
        }

        // GET: PromoCode/Delete/5
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var promo = await _context.PromoCodes.Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == id);
            if (promo == null) return NotFound();
            return View(promo);
        }

        // POST: PromoCode/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo != null)
            {
                // Invoices keep their snapshot text; the FK is SET NULL on delete.
                _context.PromoCodes.Remove(promo);
                await _context.SaveChangesAsync();
                SetSuccessMessage("Promo code deleted.");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
