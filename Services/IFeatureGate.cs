using Cafe.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cafe.Services
{
    /// <summary>
    /// Decides whether the current tenant's plan unlocks a given feature. Used by the
    /// <c>[RequireFeature]</c> server guard and by the sidebar to hide locked modules.
    /// Plan→features is cached in memory and invalidated when a plan/tenant changes.
    /// </summary>
    public interface IFeatureGate
    {
        bool HasFeature(string featureKey);
        Task<bool> HasFeatureAsync(string featureKey);
        Task<HashSet<string>> GetEnabledFeaturesAsync();
        void InvalidateForTenant(int tenantId);
    }

    public class FeatureGate : IFeatureGate
    {
        private readonly ITenantContext _tenant;
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        public FeatureGate(ITenantContext tenant, ApplicationDbContext db, IMemoryCache cache)
        {
            _tenant = tenant;
            _db = db;
            _cache = cache;
        }

        private static string CacheKey(int tenantId) => $"tenant-features-{tenantId}";

        public bool HasFeature(string featureKey) =>
            HasFeatureAsync(featureKey).GetAwaiter().GetResult();

        public async Task<bool> HasFeatureAsync(string featureKey)
        {
            if (FeatureCatalog.Core.Contains(featureKey)) return true;

            // Platform admin (not impersonating) can reach everything.
            if (_tenant.IsPlatformAdmin && !_tenant.IsImpersonating) return true;

            var features = await GetEnabledFeaturesAsync();
            return features.Contains("*") || features.Contains(featureKey);
        }

        public async Task<HashSet<string>> GetEnabledFeaturesAsync()
        {
            var tenantId = _tenant.CurrentTenantId;
            if (tenantId is null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_cache.TryGetValue(CacheKey(tenantId.Value), out HashSet<string>? cached) && cached != null)
                return cached;

            // Tenant + Plan are not tenant-owned / are looked up by id, but bypass to be safe.
            string? featuresCsv;
            using (_tenant.BypassFilter())
            {
                featuresCsv = await _db.Tenants
                    .Where(t => t.Id == tenantId.Value && t.PlanId != null)
                    .Join(_db.Plans, t => t.PlanId, p => p.Id, (t, p) => p.Features)
                    .FirstOrDefaultAsync();
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in (featuresCsv ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(key);

            _cache.Set(CacheKey(tenantId.Value), set, TimeSpan.FromMinutes(10));
            return set;
        }

        public void InvalidateForTenant(int tenantId) => _cache.Remove(CacheKey(tenantId));
    }
}
