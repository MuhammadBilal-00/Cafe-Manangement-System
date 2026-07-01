using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    /// <summary>Result of validating a promo code against a cart at checkout.</summary>
    public class PromoValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PromoCodeId { get; set; }
        public string? Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NewSubtotal { get; set; }
    }

    /// <summary>
    /// The full money breakdown for a checkout, computed by stacking discounts in a
    /// fixed order: subtotal → promo → bank partnership (on the post-promo amount) → tax.
    /// </summary>
    public class CheckoutPricing
    {
        public decimal Subtotal { get; set; }

        public int? PromoCodeId { get; set; }
        public string? PromoCodeText { get; set; }
        public decimal PromoDiscount { get; set; }
        public string? PromoMessage { get; set; }

        public int? PartnershipId { get; set; }
        public string? PartnershipText { get; set; }
        public decimal PartnershipDiscount { get; set; }
        public string? PartnershipMessage { get; set; }

        // Phase 1: POS charge lines added after discounts, before tax.
        public decimal PackingCharge { get; set; }
        public decimal ShippingCharge { get; set; }

        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }

        public decimal TotalDiscount => PromoDiscount + PartnershipDiscount;
    }

    /// <summary>Lightweight partnership shape for the POS "active partnerships" dropdown.</summary>
    public class PartnershipOption
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public decimal DiscountPercentage { get; set; }
    }
}
