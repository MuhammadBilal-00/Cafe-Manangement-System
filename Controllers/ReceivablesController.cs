using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>Phase 4: customer receivables (AR) and supplier payables (AP) dashboards + payments.</summary>
    [RequireFeature("Analytics")]
    [RequireManagerOrOwner]
    public class ReceivablesController : BaseController
    {
        private readonly IReceivablesService _receivables;
        private readonly IAuditLogService _audit;

        public ReceivablesController(ApplicationDbContext context, IReceivablesService receivables, IAuditLogService audit) : base(context)
        {
            _receivables = receivables;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Customers = await _receivables.CustomersWithDueAsync();
            ViewBag.Suppliers = await _receivables.SuppliersWithDueAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveCustomerPayment(int customerId, decimal amount, string method, string? reference)
        {
            var (ok, msg) = await _receivables.ReceiveCustomerPaymentAsync(customerId, amount, string.IsNullOrWhiteSpace(method) ? "Cash" : method, reference);
            if (ok) await _audit.LogAsync("Payment", "Customer", customerId, $"Received Rs. {amount:N0}");
            return Json(new { success = ok, message = msg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordSupplierPayment(int supplierId, decimal amount, string method, string? reference)
        {
            var (ok, msg) = await _receivables.RecordSupplierPaymentAsync(supplierId, amount, string.IsNullOrWhiteSpace(method) ? "Cash" : method, reference,
                GetEffectiveBranchId(null), GetCurrentUserId());
            if (ok) await _audit.LogAsync("Payment", "Supplier", supplierId, $"Paid Rs. {amount:N0}");
            return Json(new { success = ok, message = msg });
        }
    }
}
