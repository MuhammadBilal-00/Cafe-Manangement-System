using Cafe.Data;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Middleware
{
    /// <summary>
    /// Resolves the active tenant for every request and populates the scoped
    /// <see cref="ITenantContext"/> BEFORE the auth gate, so all data access downstream is
    /// automatically isolated. Resolution order (first match wins):
    ///   1. Platform admin session that is NOT impersonating → no tenant scope (sees all).
    ///   2. Platform admin session that IS impersonating → scoped to the impersonated tenant.
    ///   3. Authenticated session tenant id (the reliable path; works on localhost).
    ///   4. Subdomain slug ({slug}.host) or custom domain.
    ///   5. X-Tenant header (slug) for API clients.
    ///   6. None → filter bypassed (login / public / seeding).
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ApplicationDbContext db)
        {
            var session = context.Session;

            // 1 & 2 — platform admin
            if (session.IsPlatformAdmin())
            {
                var impersonated = session.GetImpersonatedTenantId();
                if (impersonated.HasValue)
                    tenantContext.BeginImpersonation(impersonated.Value);
                else
                    tenantContext.SetTenant(null, isPlatformAdmin: true);

                await _next(context);
                return;
            }

            // 3 — authenticated tenant user
            var sessionTenant = session.GetTenantId();
            if (sessionTenant.HasValue)
            {
                tenantContext.SetTenant(sessionTenant.Value, isPlatformAdmin: false);
                await _next(context);
                return;
            }

            // 4 & 5 — resolve from subdomain / custom domain / header (pre-login or API).
            //    Tenant lookups bypass the filter (Tenant is not tenant-owned, but be explicit).
            var slug = ExtractSubdomain(context.Request.Host.Host)
                       ?? context.Request.Headers["X-Tenant"].FirstOrDefault();
            var host = context.Request.Host.Host;

            if (!string.IsNullOrWhiteSpace(slug) || !string.IsNullOrWhiteSpace(host))
            {
                using (tenantContext.BypassFilter())
                {
                    var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t =>
                        (slug != null && t.Slug == slug) || t.CustomDomain == host);

                    if (tenant != null && tenant.Status != "Suspended")
                    {
                        tenantContext.SetTenant(tenant.Id, isPlatformAdmin: false);
                        await _next(context);
                        return;
                    }
                }
            }

            // 6 — no tenant resolved: leave filter bypassed (login/public). Data endpoints are
            //     still gated by AuthenticationMiddleware, so this never leaks tenant data.
            tenantContext.SetTenant(null, isPlatformAdmin: false);
            await _next(context);
        }

        /// <summary>Returns the left-most label of a multi-label host when it isn't a bare/known root.
        /// e.g. "demo.localhost" → "demo", "acme.yourbrand.com" → "acme". Returns null for "localhost",
        /// "www", IPs, or apex domains.</summary>
        private static string? ExtractSubdomain(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            if (System.Net.IPAddress.TryParse(host, out _)) return null;

            var labels = host.Split('.');
            // localhost subdomain dev: "demo.localhost" (2 labels, last == localhost)
            if (labels.Length == 2 && labels[1].Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return Normalize(labels[0]);
            // real domain: need at least sub.domain.tld (3+ labels)
            if (labels.Length >= 3)
                return Normalize(labels[0]);
            return null;
        }

        private static string? Normalize(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            if (label.Equals("www", StringComparison.OrdinalIgnoreCase)) return null;
            return label.ToLowerInvariant();
        }
    }
}
