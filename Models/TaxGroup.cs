using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 4: a named set of taxes (e.g. "GST+PST") that can stack — replacing the single branch
    /// tax rate when selected. Compound taxes apply on top of the running total.
    /// </summary>
    public class TaxGroup : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(60)] public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public ICollection<Tax> Taxes { get; set; } = new List<Tax>();
    }

    public class Tax : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int TaxGroupId { get; set; }
        [Required][StringLength(60)] public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")] public decimal Rate { get; set; }

        /// <summary>When true, this tax is charged on (base + earlier taxes) rather than the base alone.</summary>
        public bool IsCompound { get; set; } = false;
        public int SortOrder { get; set; } = 0;

        [ForeignKey("TaxGroupId")] public TaxGroup? TaxGroup { get; set; }
    }
}
