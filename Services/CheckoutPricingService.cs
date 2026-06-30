using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface ICheckoutPricingService
    {
        /// <summary>Validate a promo code against a cart total at a branch and compute its discount.</summary>
        Task<PromoValidationResult> ValidatePromoAsync(string? code, int branchId, decimal subtotal);

        /// <summary>Partnerships currently active and within their date window for this branch (auto-expired excluded).</summary>
        Task<List<Partnership>> GetActivePartnershipsAsync(int branchId);

        /// <summary>A single partnership, only if it is currently valid for this branch.</summary>
        Task<Partnership?> GetValidPartnershipAsync(int partnershipId, int branchId);

        decimal CalculatePromoDiscount(PromoCode promo, decimal subtotal);
        decimal CalculatePartnershipDiscount(Partnership partnership, decimal amount);

        /// <summary>
        /// Full money breakdown for a checkout. Invalid promo/partnership inputs are ignored
        /// (with a message) rather than throwing, so a stale code never blocks the sale.
        /// </summary>
        Task<CheckoutPricing> ComputePricingAsync(int branchId, decimal subtotal, string? promoCode, int? partnershipId);
    }

    public class CheckoutPricingService : ICheckoutPricingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBranchSettingService _branchSettings;

        public CheckoutPricingService(ApplicationDbContext context, IBranchSettingService branchSettings)
        {
            _context = context;
            _branchSettings = branchSettings;
        }

        public async Task<PromoValidationResult> ValidatePromoAsync(string? code, int branchId, decimal subtotal)
        {
            var result = new PromoValidationResult { NewSubtotal = subtotal };

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Message = "Enter a promo code.";
                return result;
            }

            var normalized = code.Trim();
            var promo = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Code == normalized);

            if (promo == null)
            {
                result.Message = "Promo code not found.";
                return result;
            }

            if (!promo.IsActive)
            {
                result.Message = "This promo code is not active.";
                return result;
            }

            var now = DateTime.Now;
            if (now < promo.ValidFrom)
            {
                result.Message = $"This code is valid from {promo.ValidFrom:dd MMM yyyy}.";
                return result;
            }
            if (now > promo.ValidUntil)
            {
                result.Message = "This promo code has expired.";
                return result;
            }

            if (promo.BranchId.HasValue && promo.BranchId.Value != branchId)
            {
                result.Message = "This code is not valid at this branch.";
                return result;
            }

            if (promo.UsageLimit.HasValue && promo.TimesUsed >= promo.UsageLimit.Value)
            {
                result.Message = "This code has reached its usage limit.";
                return result;
            }

            if (subtotal < promo.MinimumOrderAmount)
            {
                result.Message = $"Requires a minimum order of Rs. {promo.MinimumOrderAmount:N0}.";
                return result;
            }

            var discount = CalculatePromoDiscount(promo, subtotal);
            result.Success = true;
            result.PromoCodeId = promo.Id;
            result.Code = promo.Code;
            result.DiscountAmount = discount;
            result.NewSubtotal = Math.Max(0, subtotal - discount);
            result.Message = promo.DiscountType == "Percentage"
                ? $"{promo.DiscountValue:0.##}% off applied (−Rs. {discount:N0})."
                : $"Rs. {discount:N0} off applied.";
            return result;
        }

        public async Task<List<Partnership>> GetActivePartnershipsAsync(int branchId)
        {
            var now = DateTime.Now;
            return await _context.Partnerships
                .Where(p => p.IsActive
                    && p.ValidFrom <= now
                    && p.ValidUntil >= now
                    && (p.BranchId == null || p.BranchId == branchId))
                .OrderBy(p => p.PartnerName).ThenBy(p => p.CardTier)
                .ToListAsync();
        }

        public async Task<Partnership?> GetValidPartnershipAsync(int partnershipId, int branchId)
        {
            var now = DateTime.Now;
            return await _context.Partnerships
                .FirstOrDefaultAsync(p => p.Id == partnershipId
                    && p.IsActive
                    && p.ValidFrom <= now
                    && p.ValidUntil >= now
                    && (p.BranchId == null || p.BranchId == branchId));
        }

        public decimal CalculatePromoDiscount(PromoCode promo, decimal subtotal)
        {
            decimal discount;
            if (promo.DiscountType == "Percentage")
            {
                discount = subtotal * (promo.DiscountValue / 100m);
                if (promo.MaxDiscountAmount.HasValue)
                    discount = Math.Min(discount, promo.MaxDiscountAmount.Value);
            }
            else // Flat
            {
                discount = promo.DiscountValue;
            }

            discount = Math.Min(discount, subtotal); // never exceed the cart
            return Math.Round(Math.Max(0, discount), 2);
        }

        public decimal CalculatePartnershipDiscount(Partnership partnership, decimal amount)
        {
            var discount = amount * (partnership.DiscountPercentage / 100m);
            if (partnership.MaxDiscountAmount.HasValue)
                discount = Math.Min(discount, partnership.MaxDiscountAmount.Value);
            discount = Math.Min(discount, amount);
            return Math.Round(Math.Max(0, discount), 2);
        }

        public async Task<CheckoutPricing> ComputePricingAsync(int branchId, decimal subtotal, string? promoCode, int? partnershipId)
        {
            var pricing = new CheckoutPricing { Subtotal = Math.Round(subtotal, 2) };

            // ── 1. Promo discount (on the raw subtotal) ──
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var promoResult = await ValidatePromoAsync(promoCode, branchId, subtotal);
                if (promoResult.Success)
                {
                    pricing.PromoCodeId = promoResult.PromoCodeId;
                    pricing.PromoCodeText = promoResult.Code;
                    pricing.PromoDiscount = promoResult.DiscountAmount;
                }
                pricing.PromoMessage = promoResult.Message;
            }

            var afterPromo = pricing.Subtotal - pricing.PromoDiscount;

            // ── 2. Bank partnership discount (on the post-promo amount) ──
            if (partnershipId.HasValue)
            {
                var partnership = await GetValidPartnershipAsync(partnershipId.Value, branchId);
                if (partnership != null)
                {
                    pricing.PartnershipId = partnership.Id;
                    pricing.PartnershipText = $"{partnership.DisplayName} ({partnership.DiscountPercentage:0.##}%)";
                    pricing.PartnershipDiscount = CalculatePartnershipDiscount(partnership, afterPromo);
                }
                else
                {
                    pricing.PartnershipMessage = "Selected card partnership is no longer valid.";
                }
            }

            var afterDiscounts = afterPromo - pricing.PartnershipDiscount;

            // ── 3. Tax (on the net, post-discount amount) ──
            var setting = await _branchSettings.GetOrCreateAsync(branchId);
            pricing.TaxRate = setting.TaxRatePercent;
            pricing.TaxAmount = Math.Round(afterDiscounts * (setting.TaxRatePercent / 100m), 2);

            pricing.Total = Math.Round(afterDiscounts + pricing.TaxAmount, 2);
            return pricing;
        }
    }
}
