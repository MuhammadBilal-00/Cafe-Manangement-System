namespace Cafe.Models
{
    /// <summary>
    /// Marker interface for every entity that belongs to a single tenant (business).
    /// A global query filter (see <see cref="Cafe.Data.ApplicationDbContext"/>) and the
    /// <see cref="Cafe.Interceptors.TenantStampingInterceptor"/> are applied to all
    /// implementors by convention, so isolation is automatic and nothing is missed.
    ///
    /// NOTE: <see cref="User"/> and <see cref="AuditLog"/> deliberately do NOT implement this
    /// interface — they carry a *nullable* TenantId because platform-admin users and
    /// platform-level audit records exist outside any tenant. They are filtered manually.
    /// </summary>
    public interface ITenantOwned
    {
        int TenantId { get; set; }
    }
}
