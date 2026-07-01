using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class Feedback : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        // Nullable for now (no real customer auth)
        public int? CustomerId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public bool IsResolved { get; set; } = false; // legacy, not used by new status

        [StringLength(50)]
        public string? Category { get; set; } // "Service", "Food", "Drinks", etc.

        [StringLength(50)]
        public string? Source { get; set; }   // "OrderPage", "QR", "General", ...

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

        [StringLength(200)]
        public string? StaffNote { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [ForeignKey("CustomerId")]
        public User? Customer { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;
    }
}