namespace Cafe.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalBranches { get; set; }
        public int TotalMenuItems { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
        public int PendingOrders { get; set; }
        public decimal TodaysRevenue { get; set; }
        public int LowStockItems { get; set; }
        public double AverageRating { get; set; }
        public List<Order> RecentOrders { get; set; } = new List<Order>();
    }
}
