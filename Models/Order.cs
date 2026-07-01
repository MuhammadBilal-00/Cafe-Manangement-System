using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    public class Order : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        [StringLength(20)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int BranchId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Completed, Cancelled

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public User Customer { get; set; } = null!;

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // One-to-one: the immutable bill generated for this order (null until checkout completes).
        public Invoice? Invoice { get; set; }
    }
}
