using Cafe.Models;

namespace Cafe.Services
{
    public record ProvisionTenantRequest(
        string BusinessName,
        string Slug,
        string AdminName,
        string AdminEmail,
        string AdminPhone,
        string AdminPassword,
        string Template); // cafe | restaurant | bakery

    public record ProvisionResult(bool Success, Tenant? Tenant, User? Admin, string? Error);

    /// <summary>
    /// Stands up a brand-new, ready-to-use tenant in one transaction: tenant record + Trial
    /// subscription on the Free plan, a default branch, staff roles, a Walk-In customer, a starter
    /// menu template, the admin (Tenant Admin) user, and a queued welcome email.
    /// </summary>
    public interface ITenantProvisioningService
    {
        Task<ProvisionResult> ProvisionAsync(ProvisionTenantRequest request);

        /// <summary>True if the slug is free (and syntactically valid).</summary>
        Task<bool> IsSlugAvailableAsync(string slug);
    }
}
