using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } = null!; // kg, liters, pieces, packs, etc.

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal ReorderLevel { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        // Optional: you can still have extra fields if you want:
        // [StringLength(50)]
        // public string? Category { get; set; }

        // Navigation Properties
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}