using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Cafe.Models.Requests
{
    public class CreateOrderRequest
    {
        public int CustomerId { get; set; }
        public int BranchId { get; set; }
        public string? Notes { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new List<OrderItemRequest>();
    }

    public class OrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int OrderId { get; set; }
        public string NewStatus { get; set; } = "";
    }

    public class CancelOrderRequest
    {
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
