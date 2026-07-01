using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public record TrialRow(int AccountId, string Code, string Name, string Type, decimal Debit, decimal Credit);
    public record ReportLine(string Code, string Name, decimal Amount);
    public record PnL(List<ReportLine> Income, List<ReportLine> Expenses, decimal TotalIncome, decimal TotalExpense, decimal NetProfit);
    public record BalanceSheet(List<ReportLine> Assets, List<ReportLine> Liabilities, List<ReportLine> Equity,
        decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity, decimal RetainedEarnings);

    public interface IAccountingService
    {
        /// <summary>Post a balanced journal entry. Throws if debits ≠ credits.</summary>
        Task<JournalEntry> PostJournalAsync(int? branchId, DateTime date, string memo, string sourceType, int? sourceId,
            IEnumerable<(int accountId, decimal debit, decimal credit, string? desc)> lines, int? userId);

        /// <summary>Auto-post journals for the current tenant's unposted invoices &amp; expenses (idempotent).</summary>
        Task<int> AutoPostAsync(int? userId);

        Task<List<TrialRow>> TrialBalanceAsync(DateTime? from, DateTime? to);
        Task<PnL> ProfitAndLossAsync(DateTime from, DateTime to);
        Task<BalanceSheet> BalanceSheetAsync(DateTime asOf);
        Task<int?> AccountIdByCodeAsync(string code);
    }

    public class AccountingService : IAccountingService
    {
        private readonly ApplicationDbContext _db;
        public AccountingService(ApplicationDbContext db) => _db = db;

        public Task<int?> AccountIdByCodeAsync(string code) =>
            _db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefaultAsync();

        public async Task<JournalEntry> PostJournalAsync(int? branchId, DateTime date, string memo, string sourceType, int? sourceId,
            IEnumerable<(int accountId, decimal debit, decimal credit, string? desc)> lines, int? userId)
        {
            var list = lines.Where(l => l.debit != 0 || l.credit != 0).ToList();
            var dr = Math.Round(list.Sum(l => l.debit), 2);
            var cr = Math.Round(list.Sum(l => l.credit), 2);
            if (list.Count < 2) throw new InvalidOperationException("A journal needs at least two lines.");
            if (dr != cr) throw new InvalidOperationException($"Journal is unbalanced: debits {dr:N2} ≠ credits {cr:N2}.");

            var entry = new JournalEntry
            {
                BranchId = branchId, Date = date, Memo = memo, SourceType = sourceType, SourceId = sourceId,
                Status = "Posted", CreatedById = userId, CreatedAt = DateTime.Now
            };
            _db.JournalEntries.Add(entry);
            await _db.SaveChangesAsync();
            foreach (var l in list)
                _db.JournalLines.Add(new JournalLine { JournalEntryId = entry.Id, AccountId = l.accountId, Debit = Math.Round(l.debit, 2), Credit = Math.Round(l.credit, 2), Description = l.desc });
            await _db.SaveChangesAsync();
            return entry;
        }

        public async Task<int> AutoPostAsync(int? userId)
        {
            var acc = await _db.Accounts.ToDictionaryAsync(a => a.Code, a => a.Id);
            if (!acc.ContainsKey("1000")) return 0; // no chart of accounts yet
            int Cash = acc["1000"], AR = acc["1100"], Inventory = acc["1200"], AP = acc["2000"],
                TaxPay = acc["2100"], Sales = acc["4000"], OpEx = acc["6000"];

            var posted = 0;

            // Set of already-posted (SourceType, SourceId) pairs to keep every pass idempotent.
            async Task<HashSet<int>> DoneAsync(string sourceType) =>
                (await _db.JournalEntries.Where(j => j.SourceType == sourceType && j.SourceId != null)
                    .Select(j => j.SourceId!.Value).ToListAsync()).ToHashSet();

            // ── Sales invoices: Dr Cash/AR, Cr Sales (+ Cr Tax Payable) ──
            var doneInvSet = await DoneAsync("Invoice");
            var invoices = await _db.Invoices.Where(i => i.PaymentStatus == "Paid" || i.PaymentStatus == "Pending").ToListAsync();
            foreach (var inv in invoices.Where(i => !doneInvSet.Contains(i.Id)))
            {
                var net = Math.Round(inv.TotalAmount - inv.TaxAmount, 2);
                var debitAccount = inv.PaymentStatus == "Paid" ? Cash : AR;
                var lines = new List<(int, decimal, decimal, string?)>
                {
                    (debitAccount, inv.TotalAmount, 0, $"Invoice {inv.InvoiceNumber}"),
                    (Sales, 0, net, "Sales revenue")
                };
                if (inv.TaxAmount > 0) lines.Add((TaxPay, 0, inv.TaxAmount, "Tax payable"));
                await PostJournalAsync(inv.BranchId, inv.CreatedAt, $"Invoice {inv.InvoiceNumber}", "Invoice", inv.Id, lines, userId);
                posted++;
            }

            // ── Approved expenses: Dr OpEx, Cr Cash ──
            var doneExp = await DoneAsync("Expense");
            var expenses = await _db.Expenses.Where(e => e.ApprovalStatus == "Approved").ToListAsync();
            foreach (var e in expenses.Where(x => !doneExp.Contains(x.Id)))
            {
                await PostJournalAsync(e.BranchId, e.ExpenseDate, $"Expense: {e.Title}", "Expense", e.Id, new List<(int, decimal, decimal, string?)>
                {
                    (OpEx, e.Amount, 0, e.Category),
                    (Cash, 0, e.Amount, "Paid")
                }, userId);
                posted++;
            }

            // ── Stock purchases (received/approved): Dr Inventory, Cr Accounts Payable (accrued liability) ──
            var donePur = await DoneAsync("Purchase");
            var purchases = await _db.Purchases.Where(p => p.Status == "Received" || p.Status == "Approved").ToListAsync();
            foreach (var p in purchases.Where(x => !donePur.Contains(x.Id) && x.TotalCost > 0))
            {
                await PostJournalAsync(p.BranchId, p.DatePurchased, $"Purchase: {p.SupplierName}", "Purchase", p.Id, new List<(int, decimal, decimal, string?)>
                {
                    (Inventory, p.TotalCost, 0, "Stock received"),
                    (AP, 0, p.TotalCost, p.SupplierName)
                }, userId);
                posted++;
            }

            // ── Supplier payments: Dr Accounts Payable, Cr Cash (settles the liability) ──
            var donePay = await DoneAsync("SupplierPayment");
            var supPays = await _db.SupplierPayments.ToListAsync();
            foreach (var sp in supPays.Where(x => !donePay.Contains(x.Id) && x.Amount > 0))
            {
                await PostJournalAsync(sp.BranchId, sp.PaidAt, "Supplier payment", "SupplierPayment", sp.Id, new List<(int, decimal, decimal, string?)>
                {
                    (AP, sp.Amount, 0, sp.Reference ?? "Paid to supplier"),
                    (Cash, 0, sp.Amount, sp.Method)
                }, userId);
                posted++;
            }

            // ── Approved sell returns: Dr Sales (contra-revenue), Cr Cash/AR (refund/credit) ──
            var doneSR = await DoneAsync("SellReturn");
            var sellReturns = await _db.SellReturns.Where(r => r.Status == "Approved").ToListAsync();
            foreach (var r in sellReturns.Where(x => !doneSR.Contains(x.Id) && x.TotalAmount > 0))
            {
                var creditAccount = r.CustomerId.HasValue ? AR : Cash; // credit account owed vs. cash refunded
                await PostJournalAsync(r.BranchId, r.ApprovedAt ?? r.CreatedAt, $"Sell return {r.ReturnNumber}", "SellReturn", r.Id, new List<(int, decimal, decimal, string?)>
                {
                    (Sales, r.TotalAmount, 0, "Returned goods"),
                    (creditAccount, 0, r.TotalAmount, "Customer credit/refund")
                }, userId);
                posted++;
            }

            // ── Approved purchase returns: Dr Accounts Payable, Cr Inventory (stock sent back) ──
            var donePR = await DoneAsync("PurchaseReturn");
            var purReturns = await _db.PurchaseReturns.Where(r => r.Status == "Approved").ToListAsync();
            foreach (var r in purReturns.Where(x => !donePR.Contains(x.Id) && x.TotalAmount > 0))
            {
                await PostJournalAsync(r.BranchId, r.ApprovedAt ?? r.CreatedAt, $"Purchase return {r.ReturnNumber}", "PurchaseReturn", r.Id, new List<(int, decimal, decimal, string?)>
                {
                    (AP, r.TotalAmount, 0, "Reduced payable"),
                    (Inventory, 0, r.TotalAmount, "Stock returned")
                }, userId);
                posted++;
            }

            return posted;
        }

        public async Task<List<TrialRow>> TrialBalanceAsync(DateTime? from, DateTime? to)
        {
            var q = _db.JournalLines.Where(l => l.JournalEntry!.Status == "Posted");
            if (from.HasValue) q = q.Where(l => l.JournalEntry!.Date >= from.Value);
            if (to.HasValue) q = q.Where(l => l.JournalEntry!.Date < to.Value.Date.AddDays(1));

            var grouped = await q.GroupBy(l => l.AccountId)
                .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).ToListAsync();
            var accounts = await _db.Accounts.ToDictionaryAsync(a => a.Id, a => a);
            return grouped.Where(g => accounts.ContainsKey(g.AccountId))
                .Select(g => new TrialRow(g.AccountId, accounts[g.AccountId].Code, accounts[g.AccountId].Name, accounts[g.AccountId].Type, g.Debit, g.Credit))
                .OrderBy(r => r.Code).ToList();
        }

        public async Task<PnL> ProfitAndLossAsync(DateTime from, DateTime to)
        {
            var rows = await TrialBalanceAsync(from, to);
            var income = rows.Where(r => r.Type == "Income").Select(r => new ReportLine(r.Code, r.Name, r.Credit - r.Debit)).Where(r => r.Amount != 0).ToList();
            var expense = rows.Where(r => r.Type == "Expense").Select(r => new ReportLine(r.Code, r.Name, r.Debit - r.Credit)).Where(r => r.Amount != 0).ToList();
            var ti = income.Sum(r => r.Amount); var te = expense.Sum(r => r.Amount);
            return new PnL(income, expense, ti, te, ti - te);
        }

        public async Task<BalanceSheet> BalanceSheetAsync(DateTime asOf)
        {
            var rows = await TrialBalanceAsync(null, asOf);
            var assets = rows.Where(r => r.Type == "Asset").Select(r => new ReportLine(r.Code, r.Name, r.Debit - r.Credit)).Where(r => r.Amount != 0).ToList();
            var liab = rows.Where(r => r.Type == "Liability").Select(r => new ReportLine(r.Code, r.Name, r.Credit - r.Debit)).Where(r => r.Amount != 0).ToList();
            var equity = rows.Where(r => r.Type == "Equity").Select(r => new ReportLine(r.Code, r.Name, r.Credit - r.Debit)).Where(r => r.Amount != 0).ToList();
            var income = rows.Where(r => r.Type == "Income").Sum(r => r.Credit - r.Debit);
            var expense = rows.Where(r => r.Type == "Expense").Sum(r => r.Debit - r.Credit);
            var retained = income - expense; // current-period profit rolls into equity
            return new BalanceSheet(assets, liab, equity, assets.Sum(r => r.Amount), liab.Sum(r => r.Amount), equity.Sum(r => r.Amount) + retained, retained);
        }
    }
}
