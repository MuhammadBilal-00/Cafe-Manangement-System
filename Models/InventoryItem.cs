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
        public int Quantity { get; set; }    // int in DB

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }    // int in DB

        [Required]
        public int ReorderLevel { get; set; }    // int in DB

        public DateTime LastUpdated { get; set; } = DateTime.Now; // datetime2

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }    // decimal(10,2)

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    }
}