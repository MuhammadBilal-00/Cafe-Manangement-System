using System.ComponentModel.DataAnnotations;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 6: an editable, tokenized message template. Tokens like {CustomerName}, {OrderNumber},
    /// {Total} are substituted when rendering an email/SMS/in-app notification.
    /// </summary>
    public class NotificationTemplate : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        /// <summary>A stable key, e.g. "OrderReady", "OrderPlaced".</summary>
        [Required][StringLength(60)] public string Key { get; set; } = string.Empty;
        [Required][StringLength(120)] public string Name { get; set; } = string.Empty;

        /// <summary>Email | SMS | InApp</summary>
        [Required][StringLength(20)] public string Channel { get; set; } = "Email";

        [StringLength(200)] public string? Subject { get; set; }
        [Required] public string Body { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
