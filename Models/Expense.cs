using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class Expense : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "General"; // Utilities, Maintenance, Supplies, Rent, Bills, Marketing, General

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash"; // Cash, Card, BankTransfer, Cheque

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(200)]
        public string? ReceiptUrl { get; set; }

        public bool IsRecurring { get; set; } = false;

        [StringLength(20)]
        public string? RecurringFrequency { get; set; } // Monthly, Quarterly, Yearly

        [StringLength(20)]
        public string ApprovalStatus { get; set; } = "Approved"; // Pending, Approved, Rejected

        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [ForeignKey("ApprovedById")]
        public User? ApprovedBy { get; set; }

        [ForeignKey("CreatedById")]
        public User? CreatedBy { get; set; }
    }
}
