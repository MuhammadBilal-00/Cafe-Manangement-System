using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>Phase 2: product brand (e.g. "Nestlé", "House"). Used for POS filtering &amp; menu grouping.</summary>
    public class Brand : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
