using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 4: a price quote that can be converted into an order.</summary>
    public class Quotation : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int BranchId { get; set; }
        public int? CustomerId { get; set; }

        [Required][StringLength(30)] public string QuotationNumber { get; set; } = string.Empty;

        /// <summary>Draft | Sent | Accepted | Converted | Expired | Cancelled</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Draft";

        [Column(TypeName = "decimal(10,2)")] public decimal Subtotal { get; set; }
        [StringLength(400)] public string? Notes { get; set; }
        public DateTime ValidUntil { get; set; } = DateTime.Today.AddDays(14);
        public int? ConvertedOrderId { get; set; }

        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BranchId")] public Branch? Branch { get; set; }
        [ForeignKey("CustomerId")] public User? Customer { get; set; }
        public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }

    public class QuotationItem : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int QuotationId { get; set; }
        [Required] public int MenuItemId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;
        [Column(TypeName = "decimal(10,2)")] public decimal Price { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal LineDiscount { get; set; }

        [ForeignKey("QuotationId")] public Quotation? Quotation { get; set; }
        [ForeignKey("MenuItemId")] public MenuItem? MenuItem { get; set; }
    }
}
