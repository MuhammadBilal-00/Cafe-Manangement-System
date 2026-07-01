using Cafe.Models;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cafe.Interceptors
{
    /// <summary>
    /// Automatically stamps <c>TenantId</c> on every inserted tenant-owned entity, so callers
    /// never set it by hand and can't forget. Runs BEFORE the audit interceptor (registration
    /// order) so audit rows are stamped too. When no tenant is active (seeding, provisioning,
    /// platform-level work) it leaves explicitly-set values untouched.
    /// </summary>
    public class TenantStampingInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantContext _tenant;

        public TenantStampingInterceptor(ITenantContext tenant) => _tenant = tenant;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Stamp(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Stamp(DbContext? context)
        {
            if (context is null) return;
            var tenantId = _tenant.CurrentTenantId;
            if (tenantId is null) return; // nothing to stamp with (seed/provisioning set it explicitly)

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added) continue;

                switch (entry.Entity)
                {
                    case ITenantOwned owned when owned.TenantId == 0:
                        owned.TenantId = tenantId.Value;
                        break;
                    // User and AuditLog carry a nullable TenantId (platform rows are null).
                    case User u when u.TenantId is null:
                        u.TenantId = tenantId.Value;
                        break;
                    case AuditLog a when a.TenantId is null:
                        a.TenantId = tenantId.Value;
                        break;
                }
            }
        }
    }
}
