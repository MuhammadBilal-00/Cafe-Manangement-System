using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.Requests
{
    // CreateOrderRequest/OrderItemRequest removed: order creation is POS-only
    // (PosSaleRequest) — Order Management tracks existing orders.

    public class UpdateStatusRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid order is required.")]
        public int OrderId { get; set; }

        [Required(ErrorMessage = "New status is required.")]
        public string NewStatus { get; set; } = "";
    }

    public class CancelOrderRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid order is required.")]
        public int OrderId { get; set; }
        public string Reason { get; set; } = "";
    }

    public class QuickCustomerRequest
    {
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string Phone { get; set; } = "";
    }

    public class SalaryAdjustRequest
    {
        public int RecordId { get; set; }
        public string Type { get; set; } = "Bonus"; // Bonus or Deduction
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
    }

    public class BaseSalaryChangeRequest
    {
        public int StaffId { get; set; }
        public decimal NewBaseSalary { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>POS request to validate a promo code against the current cart.</summary>
    public class PromoValidateRequest
    {
        public string? Code { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A valid branch is required.")]
        public int BranchId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Subtotal { get; set; }
    }

    /// <summary>POS request for a full price breakdown (promo + card partnership + tax).</summary>
    public class QuoteRequest
    {
        public int BranchId { get; set; }
        public decimal Subtotal { get; set; }
        public string? PromoCode { get; set; }
        public int? PartnershipId { get; set; }
    }

    /// <summary>
    /// Generic payment-terminal webhook payload. A real terminal provider POSTs this to
    /// close (or fail) the bill. Authenticated by a shared secret, not a user session.
    /// </summary>
    public class PaymentWebhookPayload
    {
        public string? InvoiceNumber { get; set; }
        public string? Status { get; set; }      // "success" | "failed"
        public string? Reference { get; set; }   // terminal transaction id
        public string? Secret { get; set; }      // shared secret (or send via X-Webhook-Secret header)
    }
}
