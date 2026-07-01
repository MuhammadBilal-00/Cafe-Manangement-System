using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>Phase 6: an outbound SMS/WhatsApp message, delivered by a background sender via ISmsProvider.</summary>
    public class SmsQueue : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(30)] public string ToPhone { get; set; } = string.Empty;
        [Required][StringLength(1000)] public string Body { get; set; } = string.Empty;
        public bool IsSent { get; set; } = false;
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
        [StringLength(500)] public string? ErrorMessage { get; set; }
    }
}
