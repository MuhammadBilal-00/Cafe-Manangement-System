using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    public class NotificationPreference : ITenantOwned
    {
        public int Id { get; set; }
        // ── Multi-tenant isolation (Phase 0) ──
        public int TenantId { get; set; }

        [Required]
        public int UserId { get; set; }

        /// <summary>Receive in-app notifications</summary>
        public bool InAppEnabled { get; set; } = true;

        /// <summary>Receive email notifications</summary>
        public bool EmailEnabled { get; set; } = true;

        /// <summary>Receive order-related notifications</summary>
        public bool OrderNotifications { get; set; } = true;

        /// <summary>Receive staff/HR notifications</summary>
        public bool StaffNotifications { get; set; } = true;

        /// <summary>Receive inventory alerts</summary>
        public bool InventoryNotifications { get; set; } = true;

        /// <summary>Receive financial/salary notifications</summary>
        public bool FinancialNotifications { get; set; } = true;

        /// <summary>Receive system/admin notifications</summary>
        public bool SystemNotifications { get; set; } = true;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
