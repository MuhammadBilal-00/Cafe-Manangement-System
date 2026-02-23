using System;
using System.Collections.Generic;

namespace Cafe.Models.ViewModels
{
    public class BranchPerformanceViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<BranchPerformanceRow> Branches { get; set; } = new();
    }

    public class BranchPerformanceRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? Location { get; set; }

        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal CompletionRate => TotalOrders == 0 ? 0 : (decimal)CompletedOrders / TotalOrders * 100m;
        // NEW: feedback-related properties
        public double AvgFeedbackRating { get; set; }
        public int OpenFeedbackCount { get; set; }
    }
}