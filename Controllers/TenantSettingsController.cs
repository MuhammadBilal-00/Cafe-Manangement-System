using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Per-tenant white-label branding (logo, brand colours, receipt header/footer, custom domain).
    /// Colours override the design-system CSS variables in _Layout, so there's no per-tenant CSS.
    /// </summary>
    [RequireOwner]
    public class TenantSettingsController : BaseController
    {
        private readonly ITenantContext _tenant;
        private readonly ITenantBrandingService _branding;
        private readonly IAuditLogService _audit;

        public TenantSettingsController(ApplicationDbContext context, ITenantContext tenant,
            ITenantBrandingService branding, IAuditLogService audit) : base(context)
        {
            _tenant = tenant;
            _branding = branding;
            _audit = audit;
        }

        public async Task<IActionResult> Branding()
        {
            if (_tenant.CurrentTenantId == null) return AccessDenied();
            ViewBag.CustomDomain = await CurrentDomainAsync();
            var model = await _branding.GetForTenantAsync(_tenant.CurrentTenantId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Branding(BrandingModel model, string? customDomain)
        {
            var tenantId = _tenant.CurrentTenantId;
            if (tenantId == null) return AccessDenied();

            await _branding.UpdateAsync(tenantId.Value, model);

            // Custom domain lives on the Tenant row (used by resolution middleware).
            using (_tenant.BypassFilter())
            {
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);
                if (tenant != null)
                {
                    var domain = string.IsNullOrWhiteSpace(customDomain) ? null : customDomain.Trim().ToLowerInvariant();
                    if (domain != null && await _context.Tenants.AnyAsync(t => t.CustomDomain == domain && t.Id != tenant.Id))
                    {
                        SetErrorMessage("That custom domain is already in use by another workspace.");
                        ViewBag.CustomDomain = tenant.CustomDomain;
                        return View(model);
                    }
                    tenant.CustomDomain = domain;
                    tenant.Name = string.IsNullOrWhiteSpace(model.BusinessName) ? tenant.Name : model.BusinessName.Trim();
                    await _context.SaveChangesAsync();
                }
            }

            await _audit.LogAsync("Update", "Tenant", tenantId.Value, "Updated branding");
            SetSuccessMessage("Branding saved. Refresh to see it applied.");
            return RedirectToAction(nameof(Branding));
        }

        private async Task<string?> CurrentDomainAsync()
        {
            var id = _tenant.CurrentTenantId;
            if (id == null) return null;
            using (_tenant.BypassFilter())
                return await _context.Tenants.Where(t => t.Id == id.Value).Select(t => t.CustomDomain).FirstOrDefaultAsync();
        }
    }
}
