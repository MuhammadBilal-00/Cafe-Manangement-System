using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// A time-bound bank/card partnership discount the owner manages dynamically
    /// (e.g. "Standard Chartered" / "Platinum" = 15% off). Nothing is hardcoded —
    /// every tier is just a row here, auto-invalidated outside its date window.
    /// </summary>
    public class Partnership
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string PartnerName { get; set; } = string.Empty; // e.g. "Standard Chartered"

        [Required]
        [StringLength(60)]
        public string CardTier { get; set; } = string.Empty;    // e.g. "Platinum"

        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; }

        /// <summary>Optional cap on the discount amount (null = uncapped).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? MaxDiscountAmount { get; set; }

        /// <summary>Cart must reach this amount for the discount to apply (0 = no minimum).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal MinimumOrderAmount { get; set; } = 0;

        public DateTime ValidFrom { get; set; } = DateTime.Now;
        public DateTime ValidUntil { get; set; } = DateTime.Now.AddMonths(1);

        public bool IsActive { get; set; } = true;

        /// <summary>Branch this partnership applies to. Null = valid at all branches.</summary>
        public int? BranchId { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }

        [NotMapped]
        public string DisplayName => $"{PartnerName} {CardTier}";

        /// <summary>True when active and the current date is inside the valid window.</summary>
        [NotMapped]
        public bool IsCurrentlyValid =>
            IsActive
            && DateTime.Now >= ValidFrom
            && DateTime.Now <= ValidUntil;
    }
}
