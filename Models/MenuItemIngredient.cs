using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class MenuItemIngredient : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        public int IngredientId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Quantity { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = "g";

        public bool IsOptional { get; set; } = false;

        [Range(0, double.MaxValue)]
        public decimal? ExtraCharge { get; set; }

        // Navigation Properties
        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;

        [ForeignKey("IngredientId")]
        public Ingredient Ingredient { get; set; } = null!;
    }

}
