using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface IInvoiceService
    {
        /// <summary>
        /// Builds (or returns the existing) immutable invoice for an order, applying the given
        /// promo/partnership, snapshotting all amounts + tax, incrementing promo usage, and
        /// generating the PDF. Idempotent: one invoice per order.
        /// </summary>
        Task<Invoice> CreateForOrderAsync(int orderId, string? promoCode, int? partnershipId,
            string paymentMethod, string paymentStatus, int? performedById, decimal? taxRateOverride = null);

        Task<Invoice?> GetByOrderIdAsync(int orderId);
        Task<Invoice?> GetByIdAsync(int invoiceId);
        Task<Invoice?> GetByNumberAsync(string invoiceNumber);
        Task<string> GenerateInvoiceNumberAsync(int branchId);

        /// <summary>Regenerates the PDF for an invoice and returns the stored relative path.</summary>
        Task<string?> EnsurePdfAsync(int invoiceId);

        /// <summary>Flip a Pending bill to Paid (idempotent). Returns false if not found or already cancelled.</summary>
        Task<bool> MarkPaidAsync(int invoiceId, string? reference);

        /// <summary>Flip a Pending bill to Failed. Returns false if not found or already Paid.</summary>
        Task<bool> MarkFailedAsync(int invoiceId, string? reason);

        /// <summary>
        /// Record one split-payment tender against an invoice and re-derive PaymentStatus from
        /// the sum of tenders (Paid once tenders cover the total, else Pending). Atomic.
        /// </summary>
        Task<PaymentResult> AddPaymentAsync(int invoiceId, string method, decimal amount, string? reference);
    }

    public record PaymentResult(bool Success, string Message, decimal TotalPaid, decimal AmountDue, string PaymentStatus);

    public class InvoiceService : IInvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICheckoutPricingService _pricing;
        private readonly IPdfInvoiceService _pdf;
        private readonly IBranchSettingService _branchSettings;
        private readonly IWebHostEnvironment _env;

        public InvoiceService(
            ApplicationDbContext context,
            ICheckoutPricingService pricing,
            IPdfInvoiceService pdf,
            IBranchSettingService branchSettings,
            IWebHostEnvironment env)
        {
            _context = context;
            _pricing = pricing;
            _pdf = pdf;
            _branchSettings = branchSettings;
            _env = env;
        }

        public Task<Invoice?> GetByOrderIdAsync(int orderId) =>
            _context.Invoices
                .Include(i => i.Order).ThenInclude(o => o.Customer)
                .Include(i => i.Branch)
                .FirstOrDefaultAsync(i => i.OrderId == orderId);

        public Task<Invoice?> GetByIdAsync(int invoiceId) =>
            _context.Invoices
                .Include(i => i.Order).ThenInclude(o => o.Customer)
                .Include(i => i.Branch)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

        public Task<Invoice?> GetByNumberAsync(string invoiceNumber) =>
            _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);

        public async Task<bool> MarkPaidAsync(int invoiceId, string? reference)
        {
            var inv = await _context.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (inv == null) return false;
            if (inv.PaymentStatus == "Cancelled") return false;
            if (inv.PaymentStatus == "Paid") return true; // idempotent — webhooks may retry

            // The terminal settled the outstanding balance: record it as a tender so Payments
            // stays the single source of truth for money received (receivables, Z-reports and
            // the ledger's AR-clearing all read Payments, not just the status flag).
            var outstanding = Math.Round(inv.TotalAmount - inv.Payments.Sum(p => p.Amount), 2);
            if (outstanding > 0)
            {
                _context.Payments.Add(new Payment
                {
                    InvoiceId = inv.Id,
                    Method = "Terminal",
                    Amount = outstanding,
                    Reference = reference,
                    PaidAt = DateTime.Now
                });
            }

            inv.PaymentStatus = "Paid";
            inv.PaidAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(reference)) inv.PaymentReference = reference;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkFailedAsync(int invoiceId, string? reason)
        {
            var inv = await _context.Invoices.FindAsync(invoiceId);
            if (inv == null) return false;
            if (inv.PaymentStatus == "Paid") return false; // a settled bill can't fail

            inv.PaymentStatus = "Failed";
            if (!string.IsNullOrWhiteSpace(reason)) inv.PaymentReference = reason;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaymentResult> AddPaymentAsync(int invoiceId, string method, decimal amount, string? reference)
        {
            if (amount <= 0)
                return new PaymentResult(false, "Amount must be greater than zero.", 0, 0, "");

            await using var tx = await _context.Database.BeginTransactionAsync();
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                return new PaymentResult(false, "Invoice not found.", 0, 0, "");
            if (invoice.PaymentStatus == "Cancelled")
                return new PaymentResult(false, "This bill has been cancelled.", 0, 0, "Cancelled");

            // Cap the recorded tender at what is actually owed — change handed back is not a
            // payment, and booking it would overstate takings and push receivables negative.
            var paidSoFar = invoice.Payments.Sum(p => p.Amount);
            var apply = Math.Min(Math.Round(amount, 2), Math.Max(0, invoice.TotalAmount - paidSoFar));
            if (apply <= 0)
                return new PaymentResult(false, "This bill is already fully paid.", paidSoFar, 0, invoice.PaymentStatus);

            _context.Payments.Add(new Payment
            {
                InvoiceId = invoice.Id,
                Method = string.IsNullOrWhiteSpace(method) ? "Cash" : method,
                Amount = apply,
                Reference = reference,
                PaidAt = DateTime.Now
            });

            var totalPaid = paidSoFar + apply;
            var due = Math.Max(0, invoice.TotalAmount - totalPaid);
            var fullyPaid = totalPaid + 0.01m >= invoice.TotalAmount;

            invoice.PaymentStatus = fullyPaid ? "Paid" : "Pending";
            if (fullyPaid && invoice.PaidAt == null) invoice.PaidAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(reference)) invoice.PaymentReference = reference;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new PaymentResult(true,
                fullyPaid ? "Payment complete." : $"Partial payment recorded. Rs. {due:N0} still due.",
                totalPaid, due, invoice.PaymentStatus);
        }

        public async Task<Invoice> CreateForOrderAsync(int orderId, string? promoCode, int? partnershipId,
            string paymentMethod, string paymentStatus, int? performedById, decimal? taxRateOverride = null)
        {
            // One invoice per order — return the existing one if checkout is retried.
            var existing = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId);
            if (existing != null)
                return existing;

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new InvalidOperationException($"Order {orderId} not found.");

            // Subtotal is net of per-line discounts (Phase 1).
            var subtotal = order.OrderItems.Sum(oi => (oi.Price * oi.Quantity) - oi.LineDiscount);
            var pricing = await _pricing.ComputePricingAsync(order.BranchId, subtotal, promoCode, partnershipId,
                order.PackingCharge, order.ShippingCharge, taxRateOverride);

            var invoice = new Invoice
            {
                OrderId = order.Id,
                InvoiceNumber = await GenerateInvoiceNumberAsync(order.BranchId),
                BranchId = order.BranchId,
                Subtotal = pricing.Subtotal,
                PromoCodeId = pricing.PromoCodeId,
                PromoCodeText = pricing.PromoCodeText,
                PromoDiscount = pricing.PromoDiscount,
                PartnershipId = pricing.PartnershipId,
                PartnershipText = pricing.PartnershipText,
                PartnershipDiscount = pricing.PartnershipDiscount,
                PackingCharge = pricing.PackingCharge,
                ShippingCharge = pricing.ShippingCharge,
                TaxRate = pricing.TaxRate,
                TaxAmount = pricing.TaxAmount,
                TotalAmount = pricing.Total,
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod,
                PaymentStatus = paymentStatus,
                PaidAt = paymentStatus == "Paid" ? DateTime.Now : (DateTime?)null,
                CreatedAt = DateTime.Now
            };

            // Count a redemption against the promo's usage limit — atomically, so two registers
            // applying the same code at once both count (a tracked += would lose one).
            if (pricing.PromoCodeId.HasValue)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE PromoCodes SET TimesUsed = TimesUsed + 1 WHERE Id = {pricing.PromoCodeId.Value}");
            }

            _context.Invoices.Add(invoice);
            // The per-tenant unique index on InvoiceNumber is the real referee — on the rare
            // concurrent clash, regenerate and retry instead of surfacing a 500 at the register.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (attempt < 3
                    && ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
                    && (sql.Number == 2601 || sql.Number == 2627))
                {
                    invoice.InvoiceNumber = await GenerateInvoiceNumberAsync(order.BranchId);
                }
            }

            // Generate the PDF after we have an Id/number; failure here must not void the sale.
            try
            {
                invoice.PdfPath = await WritePdfAsync(invoice, order);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // PDF can be regenerated later via EnsurePdfAsync; leave PdfPath null.
            }

            return invoice;
        }

        public async Task<string?> EnsurePdfAsync(int invoiceId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return null;

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .FirstOrDefaultAsync(o => o.Id == invoice.OrderId);
            if (order == null) return invoice.PdfPath;

            invoice.PdfPath = await WritePdfAsync(invoice, order);
            await _context.SaveChangesAsync();
            return invoice.PdfPath;
        }

        private async Task<string> WritePdfAsync(Invoice invoice, Order order)
        {
            var setting = await _branchSettings.GetOrCreateAsync(invoice.BranchId);
            var bytes = _pdf.GenerateInvoicePdf(invoice, order, order.Branch, setting.InvoiceFooterNote);

            var dir = Path.Combine(_env.WebRootPath, "invoices");
            Directory.CreateDirectory(dir);

            var fileName = $"{invoice.InvoiceNumber}.pdf";
            var fullPath = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes);

            return $"/invoices/{fileName}"; // web-relative path stored on the invoice
        }

        public async Task<string> GenerateInvoiceNumberAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            var letters = new string((branch?.Name ?? "INV").Where(char.IsLetter).Take(3).ToArray()).ToUpper();
            if (letters.Length == 0) letters = "INV";

            var prefix = $"INV-{letters}{DateTime.Now:yyyyMMdd}";
            var todayCount = await _context.Invoices.CountAsync(i => i.InvoiceNumber.StartsWith(prefix));

            // Unique index on InvoiceNumber is the real guard; bump the sequence on the rare clash.
            for (var seq = todayCount + 1; ; seq++)
            {
                var candidate = $"{prefix}-{seq:D4}";
                if (!await _context.Invoices.AnyAsync(i => i.InvoiceNumber == candidate))
                    return candidate;
            }
        }
    }
}
