using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Attributes;
using Cafe.Helpers;
using Cafe.Services;
using System.Diagnostics;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly INotificationService _notificationService;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger, INotificationService notificationService) : base(context)
        {
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            // Determine branch scope
            var userRole = GetCurrentUserRole();
            int? scopedBranchId = null;

            if (userRole == "BranchManager")
                scopedBranchId = HttpContext.Session.GetManagedBranchId();
            else if (userRole == "Staff")
                scopedBranchId = HttpContext.Session.GetStaffBranchId();

            // Branch-scoped queries
            var branchQuery = _context.Branches.Where(b => b.IsActive);
            if (scopedBranchId.HasValue)
                branchQuery = branchQuery.Where(b => b.Id == scopedBranchId.Value);

            var staffQuery = _context.Staff.Where(s => s.IsActive);
            if (scopedBranchId.HasValue)
                staffQuery = staffQuery.Where(s => s.BranchId == scopedBranchId.Value);

            var orderQuery = _context.Orders.AsQueryable();
            if (scopedBranchId.HasValue)
                orderQuery = orderQuery.Where(o => o.BranchId == scopedBranchId.Value);

            var inventoryQuery = _context.InventoryItems.AsQueryable();
            if (scopedBranchId.HasValue)
                inventoryQuery = inventoryQuery.Where(i => i.BranchId == scopedBranchId.Value);

            var totalBranches = await branchQuery.CountAsync();
            var totalStaff = await staffQuery.CountAsync();
            var activeStaff = await staffQuery.CountAsync(s => s.EmploymentStatus == "Active");

            // Customers are global only for Owner
            var totalCustomers = userRole == "Owner" ? await _context.Customers.CountAsync(c => c.IsActive) : 0;
            var activeCustomers = totalCustomers;

            var totalOrders = await orderQuery.CountAsync();
            var todayOrders = await orderQuery.CountAsync(o => o.OrderDate.Date == DateTime.Today);
            var pendingOrders = await orderQuery.CountAsync(o => o.Status == "Pending");
            var completedOrders = await orderQuery.CountAsync(o => o.Status == "Completed");

            var menuItemQuery = _context.MenuItems.Where(m => m.Availability);
            if (scopedBranchId.HasValue)
                menuItemQuery = menuItemQuery.Where(m => m.BranchId == scopedBranchId.Value);
            var totalMenuItems = await menuItemQuery.CountAsync();

            var completedOrderQuery = orderQuery.Where(o => o.Status == "Completed");
            var averageOrderValue = await completedOrderQuery
                .AverageAsync(o => (double?)o.TotalAmount) ?? 0;

            // Monthly revenue (last 12 months) scoped
            var twelveMonthsAgo = DateTime.Today.AddMonths(-11).Date;
            twelveMonthsAgo = new DateTime(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1);
            var monthlyRevenue = await completedOrderQuery
                .Where(o => o.OrderDate >= twelveMonthsAgo)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            var chartLabels = new List<string>();
            var chartData = new List<decimal>();
            for (int i = 0; i < 12; i++)
            {
                var date = twelveMonthsAgo.AddMonths(i);
                chartLabels.Add(date.ToString("MMM yyyy"));
                var match = monthlyRevenue.FirstOrDefault(m => m.Year == date.Year && m.Month == date.Month);
                chartData.Add(match?.Revenue ?? 0m);
            }

            // 7-day sparkline data for KPI cards
            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            var orderSparkline = await orderQuery
                .Where(o => o.OrderDate.Date >= sevenDaysAgo)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            var sparkLabels = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-6 + i)).ToList();
            var orderSparkData = sparkLabels.Select(d => orderSparkline.FirstOrDefault(x => x.Date == d)?.Count ?? 0).ToList();

            // Staff attendance sparkline (last 7 days present count)
            var attendanceSparkline = await _context.Attendances
                .Where(a => a.Date >= sevenDaysAgo &&
                            (scopedBranchId == null || a.BranchId == scopedBranchId) &&
                            (a.Status == "Present" || a.Status == "Late" || a.Status == "Work From Home" || a.Status == "Overtime"))
                .GroupBy(a => a.Date.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();
            var attendanceSparkData = sparkLabels.Select(d => attendanceSparkline.FirstOrDefault(x => x.Date == d)?.Count ?? 0).ToList();

            ViewBag.OrderSparkData = orderSparkData;
            ViewBag.AttendanceSparkData = attendanceSparkData;
            ViewBag.SparkLabels = sparkLabels.Select(d => d.ToString("ddd")).ToList();

            // Low stock alerts scoped
            var lowStockItems = await inventoryQuery
                .Include(i => i.Branch)
                .Where(i => i.Quantity <= i.ReorderLevel)
                .OrderBy(i => i.Quantity)
                .Take(5)
                .ToListAsync();

            // Recent orders scoped
            var recentOrders = await orderQuery
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Join(_context.Users,
                    order => order.CustomerId,
                    user => user.Id,
                    (order, user) => new
                    {
                        order.OrderNumber,
                        CustomerName = user.Name,
                        order.TotalAmount,
                        order.Status
                    })
                .ToListAsync();

            var dashboardData = new
            {
                TotalBranches = totalBranches,
                PopularItems = await menuItemQuery
                    .Include(m => m.Category)
                    .Take(6)
                    .ToListAsync(),
                Branches = await branchQuery.ToListAsync()
            };

            ViewBag.DashboardData = dashboardData;
            ViewBag.TotalStaff = totalStaff;
            ViewBag.ActiveStaff = activeStaff;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.ActiveCustomers = activeCustomers;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TodayOrders = todayOrders;
            ViewBag.PendingOrders = pendingOrders;
            ViewBag.CompletedOrders = completedOrders;
            ViewBag.TotalMenuItems = totalMenuItems;
            ViewBag.AverageOrderValue = averageOrderValue.ToString("0.00");
            ViewBag.RecentOrders = recentOrders;
            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartData = chartData;
            ViewBag.LowStockItems = lowStockItems;

            // ── Notification Activity Feed ──
            try
            {
                var currentUserId = GetCurrentUserId() ?? 0;
                var currentRole = userRole ?? "Staff";
                int? notifBranchId = scopedBranchId;
                var recentNotifications = await _notificationService.GetRecentAsync(currentUserId, currentRole, notifBranchId, 6);
                var unreadCount = await _notificationService.GetUnreadCountAsync(currentUserId, currentRole, notifBranchId);
                ViewBag.RecentNotifications = recentNotifications;
                ViewBag.NotifUnreadCount = unreadCount;
            }
            catch
            {
                ViewBag.RecentNotifications = new List<NotificationDto>();
                ViewBag.NotifUnreadCount = 0;
            }

            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
