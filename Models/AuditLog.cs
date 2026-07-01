using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0). Null = platform-level (no tenant). ──
        public int? TenantId { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = null!;

        public int? EntityId { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }

        public int? UserId { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [StringLength(50)]
        public string? UserRole { get; set; }

        public int? BranchId { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }
    }
}
