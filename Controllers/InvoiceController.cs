using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Bill History — browse, filter, download and (for Pending bills) confirm/fail payment.
    /// Branch-scoped: Owner sees all, Manager/Staff see only their branch.
    /// </summary>
    [RequireStaffOrAbove]
    public class InvoiceController : BaseController
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(ApplicationDbContext context, IInvoiceService invoiceService) : base(context)
        {
            _invoiceService = invoiceService;
        }

        private int? ScopedBranchId(int? requested)
        {
            var role = GetCurrentUserRole();
            if (role == "Owner") return requested;                                 // optional filter
            if (role == "BranchManager") return HttpContext.Session.GetManagedBranchId();
            if (role == "Staff") return HttpContext.Session.GetStaffBranchId();
            return null;
        }

        // GET: Invoice (Bill History)
        public async Task<IActionResult> Index(int? branchId, string? status, string? search,
            DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            const int pageSize = 20;
            var scoped = ScopedBranchId(branchId);

            var query = _context.Invoices
                .Include(i => i.Order).ThenInclude(o => o.Customer)
                .Include(i => i.Branch)
                .AsQueryable();

            if (scoped.HasValue)
                query = query.Where(i => i.BranchId == scoped.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.PaymentStatus == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(i => i.InvoiceNumber.Contains(s) || i.Order.OrderNumber.Contains(s));
            }

            if (fromDate.HasValue)
                query = query.Where(i => i.CreatedAt >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(i => i.CreatedAt < toDate.Value.Date.AddDays(1));

            // KPIs across the filtered set (before paging)
            ViewBag.TotalBills = await query.CountAsync();
            ViewBag.PaidCount = await query.CountAsync(i => i.PaymentStatus == "Paid");
            ViewBag.PendingCount = await query.CountAsync(i => i.PaymentStatus == "Pending");
            ViewBag.Collected = await query.Where(i => i.PaymentStatus == "Paid").SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

            var totalCount = (int)ViewBag.TotalBills;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = scoped;
            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            return View(invoices);
        }

        // POST: Invoice/MarkPaid/5 — cashier confirmation / terminal override
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();
            if (!CanAccessBranch(invoice.BranchId)) return AccessDenied();

            var ok = await _invoiceService.MarkPaidAsync(id, "Manual confirmation");
            if (ok) SetSuccessMessage($"Bill {invoice.InvoiceNumber} marked as paid.");
            else SetErrorMessage("Could not update this bill.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Invoice/MarkFailed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkFailed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();
            if (!CanAccessBranch(invoice.BranchId)) return AccessDenied();

            var ok = await _invoiceService.MarkFailedAsync(id, "Marked failed by staff");
            if (ok) SetSuccessMessage($"Bill {invoice.InvoiceNumber} marked as failed.");
            else SetErrorMessage("A paid bill cannot be marked failed.");
            return RedirectToAction(nameof(Index));
        }

        // POST: Invoice/Regenerate/5 — rebuild a missing/updated PDF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();
            if (!CanAccessBranch(invoice.BranchId)) return AccessDenied();

            var path = await _invoiceService.EnsurePdfAsync(id);
            if (path != null) SetSuccessMessage("Bill PDF regenerated.");
            else SetErrorMessage("Could not regenerate the PDF.");
            return RedirectToAction(nameof(Index));
        }
    }
}
