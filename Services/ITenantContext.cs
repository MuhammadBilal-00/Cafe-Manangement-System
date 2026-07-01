namespace Cafe.Services
{
    /// <summary>
    /// Scoped, per-request holder of the active tenant. The DbContext reads this live to
    /// drive global query filters, and <see cref="Cafe.Interceptors.TenantStampingInterceptor"/>
    /// reads it to stamp TenantId on insert.
    ///
    /// States:
    ///  • Normal tenant user → <see cref="CurrentTenantId"/> set, <see cref="IgnoreTenantFilter"/> false.
    ///  • Platform admin (not impersonating) → <see cref="IgnoreTenantFilter"/> true (sees everything).
    ///  • Platform admin impersonating a tenant → CurrentTenantId set, IgnoreTenantFilter false,
    ///    <see cref="IsImpersonating"/> true (fully audited).
    ///  • Unauthenticated / seeding / design-time → IgnoreTenantFilter true, CurrentTenantId null.
    /// </summary>
    public interface ITenantContext
    {
        int? CurrentTenantId { get; }
        bool IgnoreTenantFilter { get; }
        bool IsPlatformAdmin { get; }
        bool IsImpersonating { get; }

        /// <summary>Set the scope for a normal tenant user (or platform admin when tenantId is null + isPlatformAdmin).</summary>
        void SetTenant(int? tenantId, bool isPlatformAdmin);

        /// <summary>Platform admin assumes a tenant's data scope. CurrentTenantId becomes the target tenant.</summary>
        void BeginImpersonation(int tenantId);

        /// <summary>Run an action with tenant filtering disabled (e.g. cross-tenant lookups at login). Restores prior state.</summary>
        IDisposable BypassFilter();
    }

    public class TenantContext : ITenantContext
    {
        public int? CurrentTenantId { get; private set; }
        public bool IgnoreTenantFilter { get; private set; } = true; // safe default until resolved
        public bool IsPlatformAdmin { get; private set; }
        public bool IsImpersonating { get; private set; }

        public void SetTenant(int? tenantId, bool isPlatformAdmin)
        {
            CurrentTenantId = tenantId;
            IsPlatformAdmin = isPlatformAdmin;
            IsImpersonating = false;
            // Platform admin with no tenant scope sees everything; everyone else is scoped to their tenant.
            IgnoreTenantFilter = isPlatformAdmin && tenantId == null;
        }

        public void BeginImpersonation(int tenantId)
        {
            CurrentTenantId = tenantId;
            IsImpersonating = true;
            IgnoreTenantFilter = false; // scoped to the impersonated tenant — never bypass
        }

        public IDisposable BypassFilter()
        {
            var prior = (CurrentTenantId, IgnoreTenantFilter, IsPlatformAdmin, IsImpersonating);
            IgnoreTenantFilter = true;
            return new Restore(() =>
            {
                (CurrentTenantId, IgnoreTenantFilter, IsPlatformAdmin, IsImpersonating) = prior;
            });
        }

        private sealed class Restore : IDisposable
        {
            private readonly Action _onDispose;
            public Restore(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
