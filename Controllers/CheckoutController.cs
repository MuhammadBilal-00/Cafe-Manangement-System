using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models.Requests;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>
    /// POS-facing checkout API used by the cashier screen. Accessible to Staff and above
    /// (unlike the Promo/Partnership admin CRUD, which is Manager/Owner only).
    /// </summary>
    [RequireStaffOrAbove]
    public class CheckoutController : BaseController
    {
        private readonly ICheckoutPricingService _pricing;

        public CheckoutController(ApplicationDbContext context, ICheckoutPricingService pricing) : base(context)
        {
            _pricing = pricing;
        }

        // POST: /Checkout/ValidatePromo  — Module 1: validate a code against the cart.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidatePromo([FromBody] PromoValidateRequest request)
        {
            if (request == null || !ModelState.IsValid)
                return Json(new { success = false, message = "Invalid request." });

            if (!CanAccessBranch(request.BranchId))
                return Json(new { success = false, message = "Access denied to this branch." });

            var result = await _pricing.ValidatePromoAsync(request.Code, request.BranchId, request.Subtotal);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                promoCodeId = result.PromoCodeId,
                code = result.Code,
                discount = result.DiscountAmount,
                newSubtotal = result.NewSubtotal
            });
        }

        // GET: /Checkout/GetActivePartnerships?branchId=1  — Module 3: dropdown of valid card tiers.
        [HttpGet]
        public async Task<IActionResult> GetActivePartnerships(int branchId)
        {
            if (!CanAccessBranch(branchId))
                return Json(new object[0]);

            var partnerships = await _pricing.GetActivePartnershipsAsync(branchId);
            var options = partnerships.Select(p => new
            {
                id = p.Id,
                displayName = p.DisplayName,
                discountPercentage = p.DiscountPercentage
            });
            return Json(options);
        }

        // POST: /Checkout/Quote  — full price breakdown preview (subtotal → promo → card → tax → total).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Quote([FromBody] QuoteRequest request)
        {
            if (request == null || request.BranchId <= 0)
                return Json(new { success = false, message = "Invalid request." });

            if (!CanAccessBranch(request.BranchId))
                return Json(new { success = false, message = "Access denied to this branch." });

            var pricing = await _pricing.ComputePricingAsync(
                request.BranchId, request.Subtotal, request.PromoCode, request.PartnershipId);

            return Json(new
            {
                success = true,
                subtotal = pricing.Subtotal,
                promoDiscount = pricing.PromoDiscount,
                promoCodeText = pricing.PromoCodeText,
                promoMessage = pricing.PromoMessage,
                partnershipDiscount = pricing.PartnershipDiscount,
                partnershipText = pricing.PartnershipText,
                partnershipMessage = pricing.PartnershipMessage,
                taxRate = pricing.TaxRate,
                taxAmount = pricing.TaxAmount,
                total = pricing.Total
            });
        }
    }
}
