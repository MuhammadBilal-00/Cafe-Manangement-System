using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class EmailQueue : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        [StringLength(255)]
        public string ToEmail { get; set; } = string.Empty;

        [StringLength(255)]
        public string? ToName { get; set; }

        [Required]
        [StringLength(300)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsSent { get; set; } = false;

        public int RetryCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>Optional link to the notification that triggered this email</summary>
        public int? NotificationId { get; set; }

        [ForeignKey("NotificationId")]
        public Notification? Notification { get; set; }
    }
}
