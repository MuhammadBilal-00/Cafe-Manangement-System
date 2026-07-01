using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 2: unit of measure with optional conversion to a base unit
    /// (e.g. "Gram" → base "Kilogram" with factor 0.001). Used for recipes/inventory display.
    /// </summary>
    public class Unit : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        [StringLength(40)]
        public string Name { get; set; } = string.Empty;   // e.g. "Kilogram"

        [Required]
        [StringLength(12)]
        public string Abbreviation { get; set; } = string.Empty; // e.g. "kg"

        /// <summary>Base unit this converts to (null = it is a base unit).</summary>
        public int? BaseUnitId { get; set; }

        /// <summary>How many base units one of this unit equals (e.g. Gram → 0.001 kg).</summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal ConversionFactor { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        [ForeignKey("BaseUnitId")]
        public Unit? BaseUnit { get; set; }
    }
}
