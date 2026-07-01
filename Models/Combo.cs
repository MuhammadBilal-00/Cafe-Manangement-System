using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 2: a deal meal / bundle sold at one price. Expands to its component menu items for
    /// inventory deduction so stock stays correct.
    /// </summary>
    public class Combo : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(400)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }
        public ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
    }

    /// <summary>A component of a combo (menu item + quantity).</summary>
    public class ComboItem : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int ComboId { get; set; }
        [Required] public int MenuItemId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;

        [ForeignKey("ComboId")] public Combo? Combo { get; set; }
        [ForeignKey("MenuItemId")] public MenuItem? MenuItem { get; set; }
    }
}
