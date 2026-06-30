using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>
    /// Per-branch checkout configuration: tax rate, the optional-hardware terminal toggle,
    /// and an invoice footer note. Owner picks any branch; a manager sees only their own.
    /// </summary>
    [RequireManagerOrOwner]
    public class CheckoutSettingsController : BaseController
    {
        private readonly IBranchSettingService _settings;

        public CheckoutSettingsController(ApplicationDbContext context, IBranchSettingService settings) : base(context)
        {
            _settings = settings;
        }

        // GET: CheckoutSettings
        public async Task<IActionResult> Index(int? branchId)
        {
            var branches = await GetAccessibleBranches();
            ViewBag.Branches = branches;

            var effective = GetEffectiveBranchId(branchId) ?? branches.FirstOrDefault()?.Id;
            if (effective == null)
                return View(null); // no accessible branch

            if (!CanAccessBranch(effective.Value))
                return AccessDenied();

            ViewBag.SelectedBranchId = effective.Value;
            var setting = await _settings.GetOrCreateAsync(effective.Value);
            return View(setting);
        }

        // POST: CheckoutSettings/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int branchId, bool hardwareTerminalEnabled, decimal taxRatePercent, string? invoiceFooterNote)
        {
            if (!CanAccessBranch(branchId))
                return AccessDenied();

            await _settings.UpdateAsync(branchId, hardwareTerminalEnabled, taxRatePercent, invoiceFooterNote);
            SetSuccessMessage("Checkout settings saved.");
            return RedirectToAction(nameof(Index), new { branchId });
        }
    }
}
