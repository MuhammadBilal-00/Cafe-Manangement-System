using System.Text.Json;
using Cafe.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cafe.Services
{
    /// <summary>White-label branding for a tenant. Colours override the design-system CSS variables
    /// (same mechanism as the dark/light theme), so no per-tenant CSS is copy-pasted.</summary>
    public class BrandingModel
    {
        public string BusinessName { get; set; } = "Cafe Manager";
        public string? LogoUrl { get; set; }
        public string PrimaryColor { get; set; } = "#d4af37";
        public string SidebarColor { get; set; } = "#1e2a3a";
        public string ReceiptHeader { get; set; } = "Cafe Manager";
        public string ReceiptFooter { get; set; } = "Thank you for visiting!";
    }

    public interface ITenantBrandingService
    {
        /// <summary>Branding for the active tenant (defaults when there is no tenant).</summary>
        Task<BrandingModel> GetCurrentAsync();
        Task<BrandingModel> GetForTenantAsync(int tenantId);
        Task UpdateAsync(int tenantId, BrandingModel model);
        void Invalidate(int tenantId);
    }

    public class TenantBrandingService : ITenantBrandingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IMemoryCache _cache;

        public TenantBrandingService(ApplicationDbContext db, ITenantContext tenant, IMemoryCache cache)
        {
            _db = db;
            _tenant = tenant;
            _cache = cache;
        }

        private static string Key(int tenantId) => $"tenant-branding-{tenantId}";

        public Task<BrandingModel> GetCurrentAsync()
        {
            var id = _tenant.CurrentTenantId;
            return id is null ? Task.FromResult(new BrandingModel()) : GetForTenantAsync(id.Value);
        }

        public async Task<BrandingModel> GetForTenantAsync(int tenantId)
        {
            if (_cache.TryGetValue(Key(tenantId), out BrandingModel? cached) && cached != null)
                return cached;

            string? json, name;
            using (_tenant.BypassFilter())
            {
                var row = await _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => new { t.BrandingJson, t.Name })
                    .FirstOrDefaultAsync();
                json = row?.BrandingJson;
                name = row?.Name;
            }

            var model = Parse(json) ?? new BrandingModel();
            if (string.IsNullOrWhiteSpace(model.BusinessName) && name != null)
                model.BusinessName = name;

            _cache.Set(Key(tenantId), model, TimeSpan.FromMinutes(10));
            return model;
        }

        public async Task UpdateAsync(int tenantId, BrandingModel model)
        {
            using (_tenant.BypassFilter())
            {
                var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant == null) return;
                tenant.BrandingJson = JsonSerializer.Serialize(model);
                await _db.SaveChangesAsync();
            }
            Invalidate(tenantId);
        }

        public void Invalidate(int tenantId) => _cache.Remove(Key(tenantId));

        private static BrandingModel? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<BrandingModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { return null; }
        }
    }
}
