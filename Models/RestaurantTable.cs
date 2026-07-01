using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// A physical table in a branch's floor plan (Phase 1). Named RestaurantTable to avoid
    /// clashing with SQL/EF "Table". Status drives the visual floor map and dine-in seating.
    /// </summary>
    [Table("RestaurantTables")]
    public class RestaurantTable : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [StringLength(40)]
        public string Name { get; set; } = string.Empty;   // e.g. "T1", "Patio 3"

        [Range(1, 100)]
        public int Capacity { get; set; } = 4;

        [StringLength(40)]
        public string? Zone { get; set; }                   // e.g. "Main", "Patio", "Rooftop"

        /// <summary>Available | Occupied | Reserved | Dirty</summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Available";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;
    }
}
