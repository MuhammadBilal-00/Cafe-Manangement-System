using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Cafe.Services.Billing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Tenant-facing plan &amp; subscription management. The "Upgrade" page is where the
    /// <c>[RequireFeature]</c> guard sends a tenant that hits a plan-locked module.
    /// </summary>
    [RequireOwner]
    public class SubscriptionController : BaseController
    {
        private readonly ITenantContext _tenant;
        private readonly IFeatureGate _featureGate;
        private readonly IBillingProvider _billing;
        private readonly IAuditLogService _audit;

        public SubscriptionController(ApplicationDbContext context, ITenantContext tenant,
            IFeatureGate featureGate, IBillingProvider billing, IAuditLogService audit) : base(context)
        {
            _tenant = tenant;
            _featureGate = featureGate;
            _billing = billing;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            await LoadPlansAsync();
            return View();
        }

        // Target of the RequireFeature redirect.
        public async Task<IActionResult> Upgrade(string? feature)
        {
            ViewBag.LockedFeature = feature;
            await LoadPlansAsync();
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(int planId)
        {
            var tenantId = _tenant.CurrentTenantId;
            if (tenantId == null) return AccessDenied();

            Tenant? tenant;
            Plan? plan;
            using (_tenant.BypassFilter())
            {
                tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);
                plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
            }
            if (tenant == null || plan == null) return NotFound();

            var result = await _billing.StartSubscriptionAsync(tenant, plan);
            if (!result.Success)
            {
                SetErrorMessage(result.Message ?? "Could not start subscription.");
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrEmpty(result.RedirectUrl))
                return Redirect(result.RedirectUrl); // hosted provider (e.g. Stripe)

            // Manual provider: activate immediately.
            tenant.PlanId = plan.Id;
            tenant.Status = "Active";
            _context.Subscriptions.Add(new Subscription
            {
                TenantId = tenant.Id,
                PlanId = plan.Id,
                Status = "Active",
                Provider = _billing.Name,
                ExternalRef = result.ExternalRef,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            });
            await _context.SaveChangesAsync();
            _featureGate.InvalidateForTenant(tenant.Id);
            await _audit.LogAsync("Subscribe", "Subscription", tenant.Id, $"Subscribed to {plan.Name} via {_billing.Name}");

            SetSuccessMessage($"You're now on the {plan.Name} plan. {result.Message}");
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadPlansAsync()
        {
            List<Plan> plans;
            int? currentPlanId;
            using (_tenant.BypassFilter())
            {
                plans = await _context.Plans.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
                currentPlanId = await _context.Tenants
                    .Where(t => t.Id == _tenant.CurrentTenantId)
                    .Select(t => t.PlanId).FirstOrDefaultAsync();
            }
            ViewBag.Plans = plans;
            ViewBag.CurrentPlanId = currentPlanId;
            ViewBag.EnabledFeatures = await _featureGate.GetEnabledFeaturesAsync();
        }
    }
}
