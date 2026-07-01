using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// The SaaS operator's console (/platform). Platform admins run with the tenant filter OFF,
    /// so every query here sees across all tenants. Tenant data access for support is done via
    /// fully-audited impersonation, never by reading another tenant's rows ad hoc.
    /// </summary>
    [RequirePlatformAdmin]
    public class PlatformController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantProvisioningService _provisioning;
        private readonly IFeatureGate _featureGate;
        private readonly IAuditLogService _audit;

        public PlatformController(ApplicationDbContext db, ITenantProvisioningService provisioning,
            IFeatureGate featureGate, IAuditLogService audit)
        {
            _db = db;
            _provisioning = provisioning;
            _featureGate = featureGate;
            _audit = audit;
        }

        // ── Dashboard ──
        public async Task<IActionResult> Index()
        {
            ViewBag.TenantCount = await _db.Tenants.CountAsync();
            ViewBag.ActiveCount = await _db.Tenants.CountAsync(t => t.Status == "Active");
            ViewBag.TrialCount = await _db.Tenants.CountAsync(t => t.Status == "Trial");
            ViewBag.SuspendedCount = await _db.Tenants.CountAsync(t => t.Status == "Suspended");
            ViewBag.UserCount = await _db.Users.CountAsync(u => u.Role != "PlatformAdmin");
            ViewBag.OrderCount = await _db.Orders.CountAsync();
            ViewBag.PlanCount = await _db.Plans.CountAsync(p => p.IsActive);

            var recent = await _db.Tenants
                .OrderByDescending(t => t.CreatedAt)
                .Take(8)
                .Select(t => new { t.Id, t.Name, t.Slug, t.Status, t.CreatedAt })
                .ToListAsync();
            ViewBag.Recent = recent;
            return View();
        }

        // ── Tenants ──
        public async Task<IActionResult> Tenants(string? q)
        {
            var query = _db.Tenants.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(t => t.Name.Contains(q) || t.Slug.Contains(q));

            // Per-tenant counts in one round trip each (small N of tenants).
            var tenants = await query.OrderBy(t => t.Name).ToListAsync();
            var planNames = await _db.Plans.ToDictionaryAsync(p => p.Id, p => p.Name);
            var userCounts = await _db.Users.Where(u => u.TenantId != null)
                .GroupBy(u => u.TenantId).Select(g => new { Id = g.Key, C = g.Count() })
                .ToDictionaryAsync(x => x.Id!.Value, x => x.C);
            var branchCounts = await _db.Branches
                .GroupBy(b => b.TenantId).Select(g => new { Id = g.Key, C = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.C);

            ViewBag.PlanNames = planNames;
            ViewBag.UserCounts = userCounts;
            ViewBag.BranchCounts = branchCounts;
            ViewBag.Query = q;
            return View(tenants);
        }

        // ── Create tenant (platform-initiated; same provisioning path as self-serve) ──
        [HttpGet]
        public IActionResult CreateTenant() => View(new ProvisionTenantRequest("", "", "", "", "", "", "cafe"));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTenant(ProvisionTenantRequest req)
        {
            if (!ModelState.IsValid) return View(req);
            var result = await _provisioning.ProvisionAsync(req);
            if (!result.Success)
            {
                ModelState.AddModelError("", result.Error ?? "Could not create tenant.");
                return View(req);
            }
            await _audit.LogAsync("Create", "Tenant", result.Tenant!.Id, $"Platform created tenant {result.Tenant.Slug}");
            TempData["Success"] = $"Tenant \"{result.Tenant!.Name}\" created.";
            return RedirectToAction(nameof(Tenants));
        }

        // ── Suspend / Activate ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, string status)
        {
            if (status is not ("Active" or "Suspended" or "Trial"))
                return BadRequest();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            tenant.Status = status;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("StatusChange", "Tenant", tenant.Id, $"Tenant {tenant.Slug} set to {status}");
            TempData["Success"] = $"\"{tenant.Name}\" is now {status}.";
            return RedirectToAction(nameof(Tenants));
        }

        // ── Assign plan ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPlan(int id, int planId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId);
            if (tenant == null || plan == null) return NotFound();

            tenant.PlanId = plan.Id;
            await _db.SaveChangesAsync();
            _featureGate.InvalidateForTenant(tenant.Id);
            await _audit.LogAsync("Update", "Tenant", tenant.Id, $"Tenant {tenant.Slug} moved to plan {plan.Name}");
            TempData["Success"] = $"\"{tenant.Name}\" is now on the {plan.Name} plan.";
            return RedirectToAction(nameof(Tenants));
        }

        // ── Impersonation (support): assume the tenant as its Owner, fully audited ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Impersonate(int id)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            // Remember who we really are so we can return.
            HttpContext.Session.SetString("PlatformReturn", "1");
            HttpContext.Session.SetInt32("ImpersonatingTenantId", tenant.Id);
            HttpContext.Session.SetString("ImpersonatingTenantName", tenant.Name);

            // Become the tenant's Owner for the duration. Tenant resolution middleware will scope
            // all data to this tenant via the session TenantId (path 3).
            HttpContext.Session.SetString("UserRole", "Owner");
            HttpContext.Session.SetInt32("TenantId", tenant.Id);

            await _audit.LogAsync("Impersonate", "Tenant", tenant.Id,
                $"Platform admin started impersonating {tenant.Slug}");
            TempData["Success"] = $"You are now impersonating \"{tenant.Name}\".";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExitImpersonation()
        {
            var tenantId = HttpContext.Session.GetInt32("ImpersonatingTenantId");
            if (tenantId.HasValue)
                await _audit.LogAsync("ExitImpersonation", "Tenant", tenantId.Value,
                    "Platform admin stopped impersonating");

            HttpContext.Session.Remove("PlatformReturn");
            HttpContext.Session.Remove("ImpersonatingTenantId");
            HttpContext.Session.Remove("ImpersonatingTenantName");
            HttpContext.Session.Remove("TenantId");
            HttpContext.Session.SetString("UserRole", "PlatformAdmin");

            TempData["Success"] = "Returned to the platform console.";
            return RedirectToAction(nameof(Tenants));
        }

        // ── Plans ──
        public async Task<IActionResult> Plans()
        {
            var plans = await _db.Plans.OrderBy(p => p.SortOrder).ToListAsync();
            return View(plans);
        }

        [HttpGet]
        public IActionResult PlanForm(int? id)
        {
            ViewBag.AllFeatures = FeatureCatalog.All;
            if (id == null) return View(new Plan { Name = "", MaxBranches = 1, MaxUsers = 5 });
            var plan = _db.Plans.Find(id.Value);
            if (plan == null) return NotFound();
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlanForm(Plan model, string[] features, bool allFeatures)
        {
            ViewBag.AllFeatures = FeatureCatalog.All;
            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(model.Name), "Name is required.");
            if (!ModelState.IsValid) return View(model);

            model.Features = allFeatures ? "*" : string.Join(",", features ?? Array.Empty<string>());

            if (model.Id == 0)
            {
                model.CreatedAt = DateTime.UtcNow;
                _db.Plans.Add(model);
            }
            else
            {
                var existing = await _db.Plans.FirstOrDefaultAsync(p => p.Id == model.Id);
                if (existing == null) return NotFound();
                existing.Name = model.Name;
                existing.Description = model.Description;
                existing.PriceMonthly = model.PriceMonthly;
                existing.MaxBranches = model.MaxBranches;
                existing.MaxUsers = model.MaxUsers;
                existing.Features = model.Features;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Plan saved.";
            return RedirectToAction(nameof(Plans));
        }
    }
}
