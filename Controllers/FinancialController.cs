using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class FinancialController : BaseController
    {
        private readonly IFinancialService _financialService;

        public FinancialController(ApplicationDbContext context, IFinancialService financialService)
            : base(context)
        {
            _financialService = financialService;
        }

        // GET: Financial/Dashboard
        public async Task<IActionResult> Dashboard(int? branchId, int? year, int? month)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
                branchId = HttpContext.Session.GetManagedBranchId();

            var vm = await _financialService.GetDashboardAsync(targetYear, targetMonth, branchId);
            return View(vm);
        }

        // GET: Financial/Expenses
        public async Task<IActionResult> Expenses(int? branchId, string? category,
            DateTime? from, DateTime? to, int page = 1, int pageSize = 25)
        {
            var query = _context.Expenses
                .Include(e => e.Branch)
                .Include(e => e.CreatedBy)
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(e => e.BranchId == managedBranchId.Value);
            }

            if (branchId.HasValue) query = query.Where(e => e.BranchId == branchId.Value);
            if (!string.IsNullOrEmpty(category)) query = query.Where(e => e.Category == category);
            if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var totalAmount = await query.SumAsync(e => (decimal?)e.Amount) ?? 0;

            var expenses = await query
                .OrderByDescending(e => e.ExpenseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new ExpenseIndexViewModel
            {
                Expenses = expenses,
                Branches = await GetAccessibleBranches(),
                BranchId = branchId,
                Category = category,
                FromDate = from,
                ToDate = to,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalAmount = totalAmount
            };

            return View(vm);
        }

        // GET: Financial/CreateExpense
        public async Task<IActionResult> CreateExpense()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            return View(new Expense());
        }

        // POST: Financial/CreateExpense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExpense(Expense expense)
        {
            if (ModelState.IsValid)
            {
                // Enforce branch for Manager
                var userRole = GetCurrentUserRole();
                if (userRole == "BranchManager")
                {
                    var managedBranchId = HttpContext.Session.GetManagedBranchId();
                    if (!managedBranchId.HasValue || expense.BranchId != managedBranchId.Value)
                    {
                        if (managedBranchId.HasValue)
                            expense.BranchId = managedBranchId.Value;
                        else
                            return AccessDenied();
                    }
                }

                expense.CreatedById = GetCurrentUserId();
                expense.CreatedAt = DateTime.Now;

                if (expense.ApprovalStatus != "Pending")
                {
                    expense.ApprovedById = GetCurrentUserId();
                    expense.ApprovedAt = DateTime.Now;
                }

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                SetSuccessMessage("Expense added successfully!");
                return RedirectToAction(nameof(Expenses));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(expense);
        }

        // GET: Financial/EditExpense/5
        public async Task<IActionResult> EditExpense(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            // Branch isolation for Manager
            if (!CanAccessBranch(expense.BranchId))
                return AccessDenied();

            ViewBag.Branches = await GetAccessibleBranches();
            return View(expense);
        }

        // POST: Financial/EditExpense/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExpense(int id, Expense expense)
        {
            if (id != expense.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Expenses.FindAsync(id);
                    if (existing == null) return NotFound();

                    // Branch isolation for Manager
                    if (!CanAccessBranch(existing.BranchId))
                        return AccessDenied();

                    existing.Title = expense.Title;
                    existing.Description = expense.Description;
                    existing.Category = expense.Category;
                    existing.Amount = expense.Amount;
                    existing.ExpenseDate = expense.ExpenseDate;
                    existing.PaymentMethod = expense.PaymentMethod;
                    existing.ReferenceNumber = expense.ReferenceNumber;
                    existing.IsRecurring = expense.IsRecurring;
                    existing.RecurringFrequency = expense.RecurringFrequency;

                    await _context.SaveChangesAsync();

                    SetSuccessMessage("Expense updated successfully!");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Expenses.Any(e => e.Id == id))
                        return NotFound();
                    throw;
                }

                return RedirectToAction(nameof(Expenses));
            }

            ViewBag.Branches = await GetAccessibleBranches();
            return View(expense);
        }

        // POST: Financial/DeleteExpense/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                SetSuccessMessage("Expense deleted successfully!");
            }

            return RedirectToAction(nameof(Expenses));
        }

        // CSV export for expenses
        public async Task<IActionResult> ExportExpensesCsv(int? branchId, DateTime? from, DateTime? to)
        {
            var query = _context.Expenses.Include(e => e.Branch).AsQueryable();

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(e => e.BranchId == managedBranchId.Value);
            }

            if (branchId.HasValue) query = query.Where(e => e.BranchId == branchId.Value);
            if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.Date);

            var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Title,Category,Amount,Branch,Date,PaymentMethod,Reference,ApprovalStatus,IsRecurring");
            foreach (var e in expenses)
            {
                csv.AppendLine($"{EscapeCsv(e.Title)},{EscapeCsv(e.Category)},{e.Amount:F2},{EscapeCsv(e.Branch?.Name ?? "")},{e.ExpenseDate:yyyy-MM-dd},{EscapeCsv(e.PaymentMethod)},{EscapeCsv(e.ReferenceNumber ?? "")},{e.ApprovalStatus},{e.IsRecurring}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"expenses-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
