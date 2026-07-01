using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public record GiftRedeemResult(bool Success, string Message, decimal Applied, decimal RemainingBalance);

    /// <summary>Phase 6: prepaid gift cards / vouchers used as a payment method. Balance moves atomically.</summary>
    public interface IGiftCardService
    {
        Task<GiftCard> IssueAsync(decimal amount, int? customerUserId, DateTime? expiresAt);
        Task<GiftCard?> GetByCodeAsync(string code);
        Task<GiftRedeemResult> RedeemAsync(string code, decimal amount, int? invoiceId);
    }

    public class GiftCardService : IGiftCardService
    {
        private readonly ApplicationDbContext _db;
        public GiftCardService(ApplicationDbContext db) => _db = db;

        public Task<GiftCard?> GetByCodeAsync(string code) =>
            _db.GiftCards.FirstOrDefaultAsync(g => g.Code == code);

        public async Task<GiftCard> IssueAsync(decimal amount, int? customerUserId, DateTime? expiresAt)
        {
            var card = new GiftCard
            {
                Code = await GenCodeAsync(), InitialBalance = Math.Round(amount, 2), Balance = Math.Round(amount, 2),
                CustomerUserId = customerUserId, ExpiresAt = expiresAt, IsActive = true
            };
            _db.GiftCards.Add(card);
            await _db.SaveChangesAsync();
            _db.GiftCardTransactions.Add(new GiftCardTransaction { GiftCardId = card.Id, Amount = card.Balance, Note = "Issued" });
            await _db.SaveChangesAsync();
            return card;
        }

        public async Task<GiftRedeemResult> RedeemAsync(string code, decimal amount, int? invoiceId)
        {
            if (amount <= 0) return new GiftRedeemResult(false, "Amount must be positive.", 0, 0);
            await using var tx = await _db.Database.BeginTransactionAsync();
            var card = await _db.GiftCards.FirstOrDefaultAsync(g => g.Code == code);
            if (card == null) return new GiftRedeemResult(false, "Gift card not found.", 0, 0);
            if (!card.IsActive || (card.ExpiresAt.HasValue && card.ExpiresAt.Value < DateTime.Now))
                return new GiftRedeemResult(false, "Gift card is inactive or expired.", 0, card.Balance);

            var apply = Math.Min(Math.Round(amount, 2), card.Balance);
            if (apply <= 0) return new GiftRedeemResult(false, "Gift card has no balance.", 0, 0);

            card.Balance -= apply;
            _db.GiftCardTransactions.Add(new GiftCardTransaction { GiftCardId = card.Id, Amount = -apply, InvoiceId = invoiceId, Note = "Redeemed" });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return new GiftRedeemResult(true, $"Applied Rs. {apply:N0}. Balance Rs. {card.Balance:N0}.", apply, card.Balance);
        }

        private async Task<string> GenCodeAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                var code = "GC-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                if (!await _db.GiftCards.AnyAsync(g => g.Code == code)) return code;
            }
            return "GC-" + DateTime.Now.Ticks;
        }
    }
}
