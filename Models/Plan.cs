using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>
    /// A subscription plan in the platform catalogue (Free/Starter/Pro/...).
    /// Platform-global: shared across all tenants, managed by the platform admin.
    /// Feature flags are stored as a comma-separated list in <see cref="Features"/>.
    /// </summary>
    public class Plan
    {
        public int Id { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        /// <summary>Monthly price in PKR (Rs.). 0 = free.</summary>
        public decimal PriceMonthly { get; set; }

        public int MaxBranches { get; set; } = 1;

        public int MaxUsers { get; set; } = 5;

        /// <summary>
        /// Comma-separated feature keys this plan unlocks, e.g. "KDS,Tables,Loyalty".
        /// "*" means every feature. See <see cref="Cafe.Services.FeatureCatalog"/>.
        /// </summary>
        [StringLength(2000)]
        public string Features { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>Display order in the pricing list.</summary>
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
