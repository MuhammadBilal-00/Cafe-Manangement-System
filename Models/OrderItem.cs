using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class OrderItem : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        // ── Phase 1: per-line discount, kitchen routing, notes ──
        [Column(TypeName = "decimal(10,2)")]
        public decimal LineDiscount { get; set; } = 0;

        public bool SentToKitchen { get; set; } = false;

        [StringLength(300)]
        public string? Notes { get; set; }

        [NotMapped]
        public decimal Subtotal => (Quantity * Price) - LineDiscount;

        // Navigation Properties
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        [ForeignKey("MenuItemId")]
        public MenuItem MenuItem { get; set; } = null!;
    }

}

