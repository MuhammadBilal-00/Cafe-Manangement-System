using System;
using System.Collections.Generic;
using Cafe.Models;

namespace Cafe.Models.ViewModels
{
    public class SalesReportViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new List<Order>();
    }
}