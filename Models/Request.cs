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
}
