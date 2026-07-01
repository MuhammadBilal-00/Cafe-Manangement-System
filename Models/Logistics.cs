using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 9 (61): a delivery rider.</summary>
    public class Rider : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(100)] public string Name { get; set; } = string.Empty;
        [StringLength(30)] public string? Phone { get; set; }
        [StringLength(40)] public string? Vehicle { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Phase 9 (61): a delivery assignment for an order.</summary>
    public class Delivery : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required] public int OrderId { get; set; }
        public int? RiderId { get; set; }
        [StringLength(300)] public string? Address { get; set; }
        /// <summary>Pending | Assigned | PickedUp | Delivered | Failed</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Pending";
        [Column(TypeName = "decimal(10,2)")] public decimal Fee { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("OrderId")] public Order? Order { get; set; }
        [ForeignKey("RiderId")] public Rider? Rider { get; set; }
    }

    /// <summary>Phase 9 (62): a shipment linked to an order or a stock transfer.</summary>
    public class Shipment : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? OrderId { get; set; }
        public int? StockTransferId { get; set; }
        [StringLength(80)] public string? Carrier { get; set; }
        [StringLength(60)] public string? TrackingNumber { get; set; }
        /// <summary>Preparing | Shipped | InTransit | Delivered | Returned</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Preparing";
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Phase 9 (60): a configurable POS/receipt profile for a branch.</summary>
    public class PosProfile : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        [Required][StringLength(80)] public string Name { get; set; } = "Default";
        /// <summary>A5 | Thermal80</summary>
        [Required][StringLength(20)] public string PaperSize { get; set; } = "A5";
        public bool ShowLogo { get; set; } = true;
        public bool ShowTaxBreakdown { get; set; } = true;
        [StringLength(300)] public string? HeaderText { get; set; }
        [StringLength(300)] public string? FooterText { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}
