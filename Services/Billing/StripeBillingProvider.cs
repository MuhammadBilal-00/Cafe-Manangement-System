using Cafe.Models;
using Microsoft.Extensions.Configuration;

namespace Cafe.Services.Billing
{
    /// <summary>
    /// International card billing via Stripe. Ships as a stub: it surfaces the integration shape
    /// and fails closed with a clear message until Stripe keys are configured under "Stripe" in
    /// appsettings. Wiring the real SDK later does not change any caller — the plan gate is
    /// provider-agnostic.
    /// </summary>
    public class StripeBillingProvider : IBillingProvider
    {
        private readonly IConfiguration _config;

        public StripeBillingProvider(IConfiguration config) => _config = config;

        public string Name => "Stripe";

        private bool Configured => !string.IsNullOrWhiteSpace(_config["Stripe:SecretKey"]);

        public Task<CheckoutResult> StartSubscriptionAsync(Tenant tenant, Plan plan)
        {
            if (!Configured)
                return Task.FromResult(new CheckoutResult(false, null, null,
                    "Stripe is not configured. Set Stripe:SecretKey in appsettings, or use the Manual provider."));

            // TODO: create Stripe Checkout Session and return its hosted URL.
            return Task.FromResult(new CheckoutResult(false, null, null,
                "Stripe checkout is not yet implemented in this build."));
        }

        public Task<CheckoutResult> CancelSubscriptionAsync(Subscription subscription)
        {
            if (!Configured)
                return Task.FromResult(new CheckoutResult(false, null, subscription.ExternalRef,
                    "Stripe is not configured."));

            return Task.FromResult(new CheckoutResult(false, null, subscription.ExternalRef,
                "Stripe cancellation is not yet implemented in this build."));
        }
    }
}
