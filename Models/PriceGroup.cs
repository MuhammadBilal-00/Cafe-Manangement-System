using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 2: a pricing tier (e.g. "Dine-In", "Delivery", "Wholesale"). A menu item can carry a
    /// different price per group via <see cref="MenuItemPrice"/>; the base MenuItem.Price is the default.
    /// </summary>
    public class PriceGroup : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>Tiered price for a menu item within a price group.</summary>
    public class MenuItemPrice : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int MenuItemId { get; set; }
        [Required] public int PriceGroupId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [ForeignKey("MenuItemId")] public MenuItem? MenuItem { get; set; }
        [ForeignKey("PriceGroupId")] public PriceGroup? PriceGroup { get; set; }
    }
}
