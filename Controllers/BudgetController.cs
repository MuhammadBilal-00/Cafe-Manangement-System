using System.Text.Json;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 5: budgets with actual-vs-budget from posted journals.</summary>
    [RequireFeature("Analytics")]
    [RequireManagerOrOwner]
    public class BudgetController : BaseController
    {
        private readonly IAccountingService _acct;
        public BudgetController(ApplicationDbContext context, IAccountingService acct) : base(context)
        {
            _acct = acct;
        }

        private record BLine(int accountId, decimal amount);

        public async Task<IActionResult> Index()
        {
            var budgets = await _context.Budgets.Include(b => b.Lines).ThenInclude(l => l.Account).OrderByDescending(b => b.Year).ToListAsync();
            ViewBag.Accounts = await _context.Accounts.Where(a => a.IsActive && (a.Type == "Income" || a.Type == "Expense")).OrderBy(a => a.Code).ToListAsync();

            // Actuals per account for the budgeted years, from the trial balance.
            var actuals = new Dictionary<int, decimal>();
            foreach (var year in budgets.Select(b => b.Year).Distinct())
            {
                var tb = await _acct.TrialBalanceAsync(new DateTime(year, 1, 1), new DateTime(year, 12, 31));
                foreach (var r in tb)
                    actuals[r.AccountId * 10000 + year] = r.Type == "Income" ? r.Credit - r.Debit : r.Debit - r.Credit;
            }
            ViewBag.Actuals = actuals;
            return View(budgets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, int year, string lines)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            List<BLine> parsed;
            try { parsed = JsonSerializer.Deserialize<List<BLine>>(lines ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid lines." }); }
            parsed = parsed.Where(l => l.accountId > 0).ToList();

            Budget budget;
            if (id == 0) { budget = new Budget { Name = name.Trim(), Year = year }; _context.Budgets.Add(budget); }
            else
            {
                budget = await _context.Budgets.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id);
                if (budget == null) return Json(new { success = false, message = "Not found." });
                budget.Name = name.Trim(); budget.Year = year;
                _context.BudgetLines.RemoveRange(budget.Lines);
            }
            await _context.SaveChangesAsync();
            foreach (var l in parsed)
                _context.BudgetLines.Add(new BudgetLine { BudgetId = budget.Id, AccountId = l.accountId, Amount = l.amount });
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var b = await _context.Budgets.FindAsync(id);
            if (b == null) return Json(new { success = false });
            _context.Budgets.Remove(b);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
