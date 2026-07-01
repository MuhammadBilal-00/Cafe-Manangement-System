using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 6: loyalty points. Earn on a paid invoice, redeem at checkout. The signed ledger
    /// (LoyaltyTransaction) and the mirrored Customer.LoyaltyPoints balance always move together.
    /// </summary>
    public interface ILoyaltyService
    {
        Task<int> BalanceAsync(int customerUserId);
        Task EarnAsync(int customerUserId, int? invoiceId, int points, string? note);
        Task<(bool ok, string message)> RedeemAsync(int customerUserId, int points, int? invoiceId);
        /// <summary>Earn points for a paid invoice at the configured rate (1 pt / Rs.100 by default). Idempotent per invoice.</summary>
        Task EarnForInvoiceAsync(Invoice invoice);
    }

    public class LoyaltyService : ILoyaltyService
    {
        private readonly ApplicationDbContext _db;
        public LoyaltyService(ApplicationDbContext db) => _db = db;

        public async Task<int> BalanceAsync(int customerUserId) =>
            await _db.Customers.Where(c => c.UserId == customerUserId).Select(c => c.LoyaltyPoints).FirstOrDefaultAsync();

        public async Task EarnAsync(int customerUserId, int? invoiceId, int points, string? note)
        {
            if (points == 0) return;
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == customerUserId);
            if (customer == null) return;
            customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints + points);
            _db.LoyaltyTransactions.Add(new LoyaltyTransaction { CustomerUserId = customerUserId, InvoiceId = invoiceId, Points = points, Type = points >= 0 ? "Earn" : "Adjust", Note = note });
            await _db.SaveChangesAsync();
        }

        public async Task<(bool, string)> RedeemAsync(int customerUserId, int points, int? invoiceId)
        {
            if (points <= 0) return (false, "Points must be positive.");
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == customerUserId);
            if (customer == null) return (false, "Customer not found.");
            if (customer.LoyaltyPoints < points) return (false, $"Only {customer.LoyaltyPoints} points available.");
            customer.LoyaltyPoints -= points;
            _db.LoyaltyTransactions.Add(new LoyaltyTransaction { CustomerUserId = customerUserId, InvoiceId = invoiceId, Points = -points, Type = "Redeem", Note = "Redeemed at checkout" });
            await _db.SaveChangesAsync();
            return (true, $"Redeemed {points} points.");
        }

        public async Task EarnForInvoiceAsync(Invoice invoice)
        {
            if (invoice.PaymentStatus != "Paid") return;
            var customerId = await _db.Orders.Where(o => o.Id == invoice.OrderId).Select(o => o.CustomerId).FirstOrDefaultAsync();
            if (customerId == null) return;
            // Idempotent: one earn per invoice.
            if (await _db.LoyaltyTransactions.AnyAsync(l => l.InvoiceId == invoice.Id && l.Type == "Earn")) return;
            var points = (int)(invoice.TotalAmount / 100m); // 1 point per Rs.100
            if (points > 0) await EarnAsync(customerId.Value, invoice.Id, points, $"Invoice {invoice.InvoiceNumber}");
        }
    }
}
