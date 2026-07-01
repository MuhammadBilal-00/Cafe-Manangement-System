using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>
    /// Self-serve signup (public). Creates a fully-provisioned tenant and signs the new
    /// owner straight in, landing them on the setup wizard.
    /// </summary>
    public class OnboardingController : Controller
    {
        private readonly ITenantProvisioningService _provisioning;

        public OnboardingController(ITenantProvisioningService provisioning)
        {
            _provisioning = provisioning;
        }

        // GET: /Onboarding
        public IActionResult Index()
        {
            if (HttpContext.Session.IsAuthenticated())
                return RedirectToAction("Index", "Home");
            return View(new ProvisionTenantRequest("", "", "", "", "", "", "cafe"));
        }

        // POST: /Onboarding
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProvisionTenantRequest req, string confirmPassword)
        {
            // Derive slug from the business name when the user didn't type one.
            if (string.IsNullOrWhiteSpace(req.Slug))
                req = req with { Slug = TenantProvisioningService.Slugify(req.BusinessName) };

            if (string.IsNullOrWhiteSpace(req.BusinessName))
                ModelState.AddModelError(nameof(req.BusinessName), "Business name is required.");
            if (string.IsNullOrWhiteSpace(req.AdminEmail))
                ModelState.AddModelError(nameof(req.AdminEmail), "Email is required.");
            if (string.IsNullOrWhiteSpace(req.AdminPassword) || req.AdminPassword.Length < 6)
                ModelState.AddModelError(nameof(req.AdminPassword), "Password must be at least 6 characters.");
            if (req.AdminPassword != confirmPassword)
                ModelState.AddModelError(nameof(confirmPassword), "Passwords do not match.");
            else if (!await _provisioning.IsSlugAvailableAsync(req.Slug))
                ModelState.AddModelError(nameof(req.Slug), "That workspace address is taken — try another.");

            if (!ModelState.IsValid) return View(req);

            var result = await _provisioning.ProvisionAsync(req);
            if (!result.Success || result.Admin == null || result.Tenant == null)
            {
                ModelState.AddModelError("", result.Error ?? "Could not create your workspace.");
                return View(req);
            }

            // Auto-login the new owner.
            HttpContext.Session.SetInt32("UserId", result.Admin.Id);
            HttpContext.Session.SetString("UserName", result.Admin.Name);
            HttpContext.Session.SetString("UserRole", "Owner");
            HttpContext.Session.SetInt32("TenantId", result.Tenant.Id);

            TempData["Success"] = $"Welcome to {result.Tenant.Name}! Let's finish setting things up.";
            return RedirectToAction("Index", "Setup");
        }

        // AJAX slug availability check for the signup form.
        [HttpGet]
        public async Task<IActionResult> CheckSlug(string slug)
        {
            var available = await _provisioning.IsSlugAvailableAsync(slug ?? "");
            return Json(new { available, slug = TenantProvisioningService.Slugify(slug ?? "") });
        }
    }
}
