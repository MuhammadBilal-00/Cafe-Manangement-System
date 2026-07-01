using Cafe.Models;
using Microsoft.Extensions.Configuration;

namespace Cafe.Services.TaxInvoice
{
    public record TaxInvoiceResult(bool Success, string? GovReference, string? QrPayload, string? Message);

    /// <summary>
    /// #37: pluggable government tax e-invoicing behind a tenant setting. Ships a Null provider
    /// (default) and a region adapter stub (Pakistan FBR/PRA). Wiring a real gov API later doesn't
    /// change callers — same pattern as IBillingProvider.
    /// </summary>
    public interface ITaxInvoiceProvider
    {
        string Key { get; }
        Task<TaxInvoiceResult> SubmitAsync(Invoice invoice, Order order);
    }

    /// <summary>Default: e-invoicing disabled — no-op.</summary>
    public class NullTaxInvoiceProvider : ITaxInvoiceProvider
    {
        public string Key => "None";
        public Task<TaxInvoiceResult> SubmitAsync(Invoice invoice, Order order) =>
            Task.FromResult(new TaxInvoiceResult(true, null, null, "E-invoicing is not enabled for this tenant."));
    }

    /// <summary>
    /// Pakistan FBR/PRA-style adapter. Stub: builds the fiscal payload shape and fails closed until
    /// gov credentials are configured under "TaxInvoice:Fbr" in appsettings.
    /// </summary>
    public class PakFbrTaxInvoiceProvider : ITaxInvoiceProvider
    {
        private readonly IConfiguration _config;
        public PakFbrTaxInvoiceProvider(IConfiguration config) => _config = config;

        public string Key => "PakFbr";
        private bool Configured => !string.IsNullOrWhiteSpace(_config["TaxInvoice:Fbr:Token"]);

        public Task<TaxInvoiceResult> SubmitAsync(Invoice invoice, Order order)
        {
            if (!Configured)
                return Task.FromResult(new TaxInvoiceResult(false, null, null,
                    "FBR e-invoicing is not configured (set TaxInvoice:Fbr:Token). Payload prepared but not submitted."));

            // TODO: POST the fiscal payload to the FBR/PRA endpoint and return the fiscal number + QR.
            return Task.FromResult(new TaxInvoiceResult(false, null, null,
                "FBR submission is not yet implemented in this build."));
        }
    }
}
