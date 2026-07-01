using Cafe.Models;

namespace Cafe.Services.Billing
{
    /// <summary>
    /// Default provider for local (Pakistan / PKR) billing: the platform operator invoices the
    /// tenant out-of-band (bank transfer, cash) and the subscription is activated immediately in
    /// "Active" state. No external redirect. The plan gate enforces access either way.
    /// </summary>
    public class ManualBillingProvider : IBillingProvider
    {
        public string Name => "Manual";

        public Task<CheckoutResult> StartSubscriptionAsync(Tenant tenant, Plan plan)
        {
            var reference = $"MAN-{tenant.Slug}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            return Task.FromResult(new CheckoutResult(
                Success: true,
                RedirectUrl: null,
                ExternalRef: reference,
                Message: $"Manual invoice {reference} raised for {plan.Name} (Rs. {plan.PriceMonthly:N0}/mo)."));
        }

        public Task<CheckoutResult> CancelSubscriptionAsync(Subscription subscription) =>
            Task.FromResult(new CheckoutResult(true, null, subscription.ExternalRef, "Subscription cancelled."));
    }
}
