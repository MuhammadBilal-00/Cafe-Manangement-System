using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.Requests
{
    public class CreateOrderRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid customer is required.")]
        public int CustomerId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A valid branch is required.")]
        public int BranchId { get; set; }

        public string? Notes { get; set; }

        [Required(ErrorMessage = "An order must contain at least one item.")]
        [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
        public List<OrderItemRequest> Items { get; set; } = new List<OrderItemRequest>();
    }

    public class OrderItemRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid menu item is required.")]
        public int MenuItemId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }

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
}
