using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 6: gift cards / vouchers.</summary>
    [RequireFeature("Marketing")]
    [RequireManagerOrOwner]
    public class GiftCardController : BaseController
    {
        private readonly IGiftCardService _giftCards;
        private readonly IAuditLogService _audit;

        public GiftCardController(ApplicationDbContext context, IGiftCardService giftCards, IAuditLogService audit) : base(context)
        {
            _giftCards = giftCards;
            _audit = audit;
        }

        public async Task<IActionResult> Index() =>
            View(await _context.GiftCards.OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(decimal amount, DateTime? expiresAt)
        {
            if (amount <= 0) return Json(new { success = false, message = "Amount must be positive." });
            var card = await _giftCards.IssueAsync(amount, null, expiresAt);
            await _audit.LogAsync("Issue", "GiftCard", card.Id, $"Issued {card.Code} Rs. {amount:N0}");
            return Json(new { success = true, code = card.Code });
        }

        [HttpGet]
        public async Task<IActionResult> Balance(string code)
        {
            var card = await _giftCards.GetByCodeAsync(code);
            return card == null ? Json(new { found = false }) : Json(new { found = true, code = card.Code, balance = card.Balance, active = card.IsActive });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Redeem(string code, decimal amount)
        {
            var r = await _giftCards.RedeemAsync(code, amount, null);
            return Json(new { success = r.Success, message = r.Message, applied = r.Applied, balance = r.RemainingBalance });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var card = await _context.GiftCards.FindAsync(id);
            if (card == null) return Json(new { success = false });
            card.IsActive = !card.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, active = card.IsActive });
        }
    }
}
