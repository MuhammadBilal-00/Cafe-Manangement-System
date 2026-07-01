using System.Text.Json;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 5: double-entry accounting — chart of accounts, journals, auto-posting, reports.</summary>
    [RequireFeature("Analytics")]
    [RequireManagerOrOwner]
    public class AccountingController : BaseController
    {
        private readonly IAccountingService _acct;
        private readonly IAuditLogService _audit;

        public AccountingController(ApplicationDbContext context, IAccountingService acct, IAuditLogService audit) : base(context)
        {
            _acct = acct;
            _audit = audit;
        }

        private record JLine(int accountId, decimal debit, decimal credit, string? description);

        public async Task<IActionResult> Index()
        {
            ViewBag.AccountCount = await _context.Accounts.CountAsync();
            ViewBag.JournalCount = await _context.JournalEntries.CountAsync();
            var tb = await _acct.TrialBalanceAsync(null, null);
            ViewBag.TotalDebit = tb.Sum(r => r.Debit);
            ViewBag.TotalCredit = tb.Sum(r => r.Credit);
            return View();
        }

        // ── Chart of accounts ──
        public async Task<IActionResult> ChartOfAccounts() =>
            View(await _context.Accounts.OrderBy(a => a.Code).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccount(int id, string code, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Code and name are required." });
            if (type is not ("Asset" or "Liability" or "Equity" or "Income" or "Expense")) return Json(new { success = false, message = "Invalid type." });
            code = code.Trim();
            if (await _context.Accounts.AnyAsync(a => a.Code == code && a.Id != id)) return Json(new { success = false, message = "That code already exists." });
            if (id == 0) _context.Accounts.Add(new Account { Code = code, Name = name.Trim(), Type = type, IsActive = true });
            else
            {
                var a = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == id);
                if (a == null) return Json(new { success = false, message = "Not found." });
                a.Code = code; a.Name = name.Trim(); a.Type = type;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var a = await _context.Accounts.FindAsync(id);
            if (a == null) return Json(new { success = false });
            if (a.IsSystem) return Json(new { success = false, message = "System accounts can't be deleted." });
            if (await _context.JournalLines.AnyAsync(l => l.AccountId == id)) return Json(new { success = false, message = "Account has journal activity." });
            _context.Accounts.Remove(a);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // ── Journals ──
        public async Task<IActionResult> Journals()
        {
            var entries = await _context.JournalEntries.Include(j => j.Lines).ThenInclude(l => l.Account)
                .OrderByDescending(j => j.Date).ThenByDescending(j => j.Id).Take(100).ToListAsync();
            ViewBag.Accounts = await _context.Accounts.Where(a => a.IsActive).OrderBy(a => a.Code).ToListAsync();
            return View(entries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateJournal(DateTime date, string? memo, string lines)
        {
            List<JLine> parsed;
            try { parsed = JsonSerializer.Deserialize<List<JLine>>(lines ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid lines." }); }
            parsed = parsed.Where(l => l.accountId > 0 && (l.debit != 0 || l.credit != 0)).ToList();
            try
            {
                await _acct.PostJournalAsync(GetEffectiveBranchId(null), date, memo ?? "Manual entry", "Manual", null,
                    parsed.Select(l => (l.accountId, l.debit, l.credit, l.description)), GetCurrentUserId());
                await _audit.LogAsync("Post", "JournalEntry", null, memo);
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoPost()
        {
            var n = await _acct.AutoPostAsync(GetCurrentUserId());
            await _audit.LogAsync("AutoPost", "JournalEntry", null, $"Auto-posted {n} entries");
            return Json(new { success = true, message = $"Posted {n} new journal entr{(n == 1 ? "y" : "ies")} from invoices & expenses." });
        }

        // ── Reports ──
        public async Task<IActionResult> Reports(DateTime? from, DateTime? to)
        {
            var f = from ?? new DateTime(DateTime.Now.Year, 1, 1);
            var t = to ?? DateTime.Now.Date;
            ViewBag.From = f; ViewBag.To = t;
            ViewBag.Trial = await _acct.TrialBalanceAsync(f, t);
            ViewBag.PnL = await _acct.ProfitAndLossAsync(f, t);
            ViewBag.BalanceSheet = await _acct.BalanceSheetAsync(t);
            return View();
        }
    }
}
