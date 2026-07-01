using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>
    /// A tenant's active/historical subscription to a <see cref="Plan"/>.
    /// Tenant-owned (a tenant admin only sees their own subscriptions). Billing
    /// is driven through <see cref="Cafe.Services.Billing.IBillingProvider"/>.
    /// </summary>
    public class Subscription : ITenantOwned
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int PlanId { get; set; }

        /// <summary>Active | Trialing | PastDue | Cancelled</summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Trialing";

        public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;

        public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);

        /// <summary>Manual | Stripe</summary>
        [Required]
        [StringLength(30)]
        public string Provider { get; set; } = "Manual";

        /// <summary>Provider-side reference (e.g. Stripe subscription id, or a manual invoice no.).</summary>
        [StringLength(150)]
        public string? ExternalRef { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation. (TenantId FK is wired by convention in ApplicationDbContext.)
        public Plan? Plan { get; set; }
    }
}
