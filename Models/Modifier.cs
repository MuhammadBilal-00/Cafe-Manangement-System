using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 2: a group of add-ons/variations attached to menu items (e.g. "Size", "Extras").
    /// Min/Max selectable enforce choice rules at the register.
    /// </summary>
    public class ModifierGroup : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;   // e.g. "Size"

        public int MinSelect { get; set; } = 0;
        public int MaxSelect { get; set; } = 1;             // 1 = single-choice, >1 = multi
        public bool IsRequired { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public ICollection<Modifier> Modifiers { get; set; } = new List<Modifier>();
    }

    /// <summary>One selectable option within a group (e.g. "Large" at +Rs.100).</summary>
    public class Modifier : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int ModifierGroupId { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;   // e.g. "Large"

        [Column(TypeName = "decimal(10,2)")]
        public decimal PriceDelta { get; set; } = 0;        // added to the item price

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        [ForeignKey("ModifierGroupId")]
        public ModifierGroup? Group { get; set; }
    }

    /// <summary>Junction: which modifier groups apply to a menu item.</summary>
    public class MenuItemModifierGroup : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int MenuItemId { get; set; }
        [Required] public int ModifierGroupId { get; set; }

        [ForeignKey("MenuItemId")] public MenuItem? MenuItem { get; set; }
        [ForeignKey("ModifierGroupId")] public ModifierGroup? ModifierGroup { get; set; }
    }
}
