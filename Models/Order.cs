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

        // Phase 1: optional customer — walk-in sales have no customer.
        public int? CustomerId { get; set; }

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

        // ── Phase 1: POS / restaurant fields ──
        public int? TableId { get; set; }

        /// <summary>DineIn | Takeaway | Delivery</summary>
        [StringLength(20)]
        public string ServiceType { get; set; } = "DineIn";

        /// <summary>Staff member serving/assigned to this order.</summary>
        public int? ServiceStaffId { get; set; }

        /// <summary>Kitchen ticket state (KDS): New | Cooking | Ready | Served</summary>
        [StringLength(20)]
        public string KitchenStatus { get; set; } = "New";

        /// <summary>Hold/draft workflow: Active | Suspended | Draft</summary>
        [StringLength(20)]
        public string HoldState { get; set; } = "Active";

        [Column(TypeName = "decimal(10,2)")]
        public decimal PackingCharge { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ShippingCharge { get; set; } = 0;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public User? Customer { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("TableId")]
        public RestaurantTable? Table { get; set; }

        [ForeignKey("ServiceStaffId")]
        public Staff? ServiceStaff { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // One-to-one: the immutable bill generated for this order (null until checkout completes).
        public Invoice? Invoice { get; set; }
    }
}
