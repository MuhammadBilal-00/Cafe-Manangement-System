using System;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models.Requests;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cafe.Controllers
{
    /// <summary>
    /// Generic payment-terminal webhook receiver. The provider POSTs a success/failure
    /// notification here to close (or fail) a Pending bill. There is no user session —
    /// the request is authenticated by a shared secret (header X-Webhook-Secret or body).
    /// The /paymentwebhook path is whitelisted in AuthenticationMiddleware.
    /// </summary>
    public class PaymentWebhookController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInvoiceService _invoiceService;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentWebhookController> _logger;

        public PaymentWebhookController(
            ApplicationDbContext context,
            IInvoiceService invoiceService,
            INotificationService notificationService,
            IConfiguration config,
            ILogger<PaymentWebhookController> logger)
        {
            _context = context;
            _invoiceService = invoiceService;
            _notificationService = notificationService;
            _config = config;
            _logger = logger;
        }

        // POST: /PaymentWebhook/Notify
        [HttpPost]
        public async Task<IActionResult> Notify([FromBody] PaymentWebhookPayload payload)
        {
            // ── Authenticate the caller (shared secret) ──
            var expected = _config["Payments:WebhookSecret"] ?? string.Empty;
            var provided = Request.Headers["X-Webhook-Secret"].ToString();
            if (string.IsNullOrEmpty(provided)) provided = payload?.Secret ?? string.Empty;
            if (string.IsNullOrEmpty(expected) || provided != expected)
            {
                _logger.LogWarning("Payment webhook rejected: bad secret for invoice {Invoice}", payload?.InvoiceNumber);
                return Unauthorized(new { success = false, message = "Invalid webhook secret." });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.InvoiceNumber))
                return BadRequest(new { success = false, message = "invoiceNumber is required." });

            var invoice = await _invoiceService.GetByNumberAsync(payload.InvoiceNumber);
            if (invoice == null)
                return NotFound(new { success = false, message = "Invoice not found." });

            var status = (payload.Status ?? string.Empty).Trim().ToLowerInvariant();

            if (status is "success" or "succeeded" or "paid")
            {
                await _invoiceService.MarkPaidAsync(invoice.Id, payload.Reference);
                await NotifyAsync(invoice.BranchId, "Payment Received",
                    $"Bill {invoice.InvoiceNumber} was paid via terminal.", "Success", "fas fa-circle-check");
                return Json(new { success = true, invoiceNumber = invoice.InvoiceNumber, paymentStatus = "Paid" });
            }

            if (status is "failed" or "failure" or "declined")
            {
                await _invoiceService.MarkFailedAsync(invoice.Id, payload.Reference);
                await NotifyAsync(invoice.BranchId, "Payment Failed",
                    $"Card payment for bill {invoice.InvoiceNumber} was declined.", "Error", "fas fa-circle-xmark");
                return Json(new { success = true, invoiceNumber = invoice.InvoiceNumber, paymentStatus = "Failed" });
            }

            return BadRequest(new { success = false, message = "status must be 'success' or 'failed'." });
        }

        private async Task NotifyAsync(int branchId, string title, string message, string type, string icon)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    title, message, type, NotificationCategory.Financial,
                    branchId: branchId, createdBy: null,
                    redirectUrl: "/Invoice/Index", icon: icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push payment notification for branch {Branch}", branchId);
            }
        }
    }
}
