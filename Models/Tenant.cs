using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>
    /// A tenant = one business (cafe/restaurant chain) on the SaaS platform.
    /// Root of the isolation hierarchy: Tenant → Branches → all operational data.
    /// This entity itself is NOT tenant-owned (it is the tenant).
    /// </summary>
    public class Tenant
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>URL-safe subdomain identifier, e.g. "demo" → demo.yourbrand.com. Unique.</summary>
        [Required]
        [StringLength(63)]
        public string Slug { get; set; } = string.Empty;

        /// <summary>Optional vanity domain, e.g. "orders.acmecafe.com". Unique when set.</summary>
        [StringLength(253)]
        public string? CustomDomain { get; set; }

        /// <summary>Active | Trial | Suspended</summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Trial";

        public int? PlanId { get; set; }

        /// <summary>White-label branding as JSON (logo URL, brand colors, receipt header/footer).</summary>
        public string? BrandingJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation. (No navigation collections to tenant-owned entities — the FK to Tenant is
        // wired uniformly by convention in ApplicationDbContext so none is missed.)
        public Plan? Plan { get; set; }
    }
}
