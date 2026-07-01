using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 6: loyalty points ledger &amp; adjustments.</summary>
    [RequireFeature("Marketing")]
    [RequireManagerOrOwner]
    public class LoyaltyController : BaseController
    {
        private readonly ILoyaltyService _loyalty;
        private readonly IAuditLogService _audit;

        public LoyaltyController(ApplicationDbContext context, ILoyaltyService loyalty, IAuditLogService audit) : base(context)
        {
            _loyalty = loyalty;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Customers = await _context.Customers.Include(c => c.User).Where(c => c.IsActive && c.LoyaltyPoints > 0)
                .OrderByDescending(c => c.LoyaltyPoints)
                .Select(c => new { c.UserId, c.User.Name, c.User.Phone, c.LoyaltyPoints }).Take(100).ToListAsync();
            ViewBag.Ledger = await _context.LoyaltyTransactions.Include(l => l.Customer)
                .OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(int customerUserId, int points, string? note)
        {
            await _loyalty.EarnAsync(customerUserId, null, points, note ?? "Manual adjustment");
            await _audit.LogAsync("Adjust", "Loyalty", customerUserId, $"{points:+#;-#;0} points");
            return Json(new { success = true, balance = await _loyalty.BalanceAsync(customerUserId) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Redeem(int customerUserId, int points)
        {
            var (ok, msg) = await _loyalty.RedeemAsync(customerUserId, points, null);
            return Json(new { success = ok, message = msg, balance = await _loyalty.BalanceAsync(customerUserId) });
        }
    }
}
