using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>
    /// Phase 6: a loyalty points movement. Positive = earned (on a paid invoice), negative = redeemed
    /// at checkout. The running balance is mirrored on Customer.LoyaltyPoints.
    /// </summary>
    public class LoyaltyTransaction : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int CustomerUserId { get; set; }
        public int? InvoiceId { get; set; }

        /// <summary>Signed points: + earned, − redeemed.</summary>
        public int Points { get; set; }
        /// <summary>Earn | Redeem | Adjust</summary>
        [Required][StringLength(20)] public string Type { get; set; } = "Earn";
        [StringLength(200)] public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("CustomerUserId")] public User? Customer { get; set; }
    }
}
