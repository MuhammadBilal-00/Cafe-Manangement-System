using Cafe.Models;

namespace Cafe.Services.Billing
{
    public record CheckoutResult(bool Success, string? RedirectUrl, string? ExternalRef, string? Message);

    /// <summary>
    /// Abstraction over how a tenant pays for its plan. Plan gating works regardless of which
    /// provider is wired. Two implementations ship:
    ///  • <see cref="ManualBillingProvider"/> — local/Pakistan manual invoicing (default).
    ///  • <see cref="StripeBillingProvider"/> — international card billing (stub until keys are set).
    /// </summary>
    public interface IBillingProvider
    {
        string Name { get; }

        /// <summary>Start a subscription for a tenant on a plan. Returns a redirect URL when the
        /// provider is hosted (Stripe), or completes in-place (Manual).</summary>
        Task<CheckoutResult> StartSubscriptionAsync(Tenant tenant, Plan plan);

        /// <summary>Cancel the tenant's current subscription with the provider.</summary>
        Task<CheckoutResult> CancelSubscriptionAsync(Subscription subscription);
    }
}
