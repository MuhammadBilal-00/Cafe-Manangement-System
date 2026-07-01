using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 5: bank/cash registers with reconciliation.</summary>
    [RequireFeature("Analytics")]
    [RequireManagerOrOwner]
    public class PaymentAccountController : BaseController
    {
        public PaymentAccountController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            ViewBag.Accounts = await _context.Accounts.Where(a => a.Type == "Asset" && a.IsActive).OrderBy(a => a.Code).ToListAsync();
            return View(await _context.PaymentAccounts.Include(p => p.Account).OrderBy(p => p.Name).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, string type, int? accountId, decimal openingBalance, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (id == 0) _context.PaymentAccounts.Add(new PaymentAccount { Name = name.Trim(), Type = type, AccountId = accountId, OpeningBalance = openingBalance, IsActive = isActive });
            else
            {
                var p = await _context.PaymentAccounts.FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Json(new { success = false, message = "Not found." });
                p.Name = name.Trim(); p.Type = type; p.AccountId = accountId; p.OpeningBalance = openingBalance; p.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reconcile(int id, decimal statementBalance)
        {
            var p = await _context.PaymentAccounts.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return Json(new { success = false });
            p.ReconciledBalance = statementBalance;
            p.LastReconciledAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.PaymentAccounts.FindAsync(id);
            if (p == null) return Json(new { success = false });
            _context.PaymentAccounts.Remove(p);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
