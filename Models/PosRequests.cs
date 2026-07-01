using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.Requests
{
    /// <summary>A single cart line at the register.</summary>
    public class PosLineRequest
    {
        [Required] public int MenuItemId { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;
        [Range(0, double.MaxValue)] public decimal LineDiscount { get; set; } = 0;
        public string? Notes { get; set; }

        /// <summary>Phase 2: selected modifier ids; their price deltas are added server-side.</summary>
        public List<int> ModifierIds { get; set; } = new();
    }

    /// <summary>One tender in a (possibly split) payment.</summary>
    public class PosPaymentRequest
    {
        [Required] public string Method { get; set; } = "Cash";
        [Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

    /// <summary>A full POS sale/hold/draft submission from the register.</summary>
    public class PosSaleRequest
    {
        [Required] public int BranchId { get; set; }
        public int? CustomerId { get; set; }          // null = Walk-In
        public int? TableId { get; set; }
        public string ServiceType { get; set; } = "DineIn"; // DineIn | Takeaway | Delivery
        public int? ServiceStaffId { get; set; }
        public string? Notes { get; set; }

        public List<PosLineRequest> Items { get; set; } = new();

        public string? PromoCode { get; set; }
        public int? PartnershipId { get; set; }

        public decimal PackingCharge { get; set; } = 0;
        public decimal ShippingCharge { get; set; } = 0;
        public decimal? TaxRateOverride { get; set; }

        /// <summary>Phase 2: pricing tier — line base prices use this group's override where set.</summary>
        public int? PriceGroupId { get; set; }

        public List<PosPaymentRequest> Payments { get; set; } = new();

        /// <summary>Client-generated idempotency key to guard double-submits.</summary>
        public string? ClientRef { get; set; }

        /// <summary>When resuming a held/draft order, its id (finalize updates it in place).</summary>
        public int? ExistingOrderId { get; set; }
    }
}
