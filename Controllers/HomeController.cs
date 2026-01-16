using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using System.Diagnostics;

namespace Cafe.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Get accurate statistics from database
            var totalBranches = await _context.Branches.CountAsync(b => b.IsActive);
            var totalStaff = await _context.Staff.CountAsync(s => s.IsActive);
            var activeStaff = await _context.Staff.CountAsync(s => s.IsActive && s.EmploymentStatus == "Active");
            var totalCustomers = await _context.Customers.CountAsync(c => c.IsActive);
            var activeCustomers = await _context.Customers.CountAsync(c => c.IsActive);
            var totalOrders = await _context.Orders.CountAsync();
            var todayOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date == DateTime.Today);
            var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
            var completedOrders = await _context.Orders.CountAsync(o => o.Status == "Completed");
            var totalMenuItems = await _context.MenuItems.CountAsync(m => m.Availability);

            // Calculate average order value
            var averageOrderValue = await _context.Orders
                .Where(o => o.Status == "Completed")
                .AverageAsync(o => (double?)o.TotalAmount) ?? 0;

            // Get recent orders - Alternative approach if navigation properties aren't working
            var recentOrders = await _context.Orders
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
                PopularItems = await _context.MenuItems
                    .Include(m => m.Category)
                    .Where(m => m.Availability)
                    .Take(6)
                    .ToListAsync(),
                Branches = await _context.Branches
                    .Where(b => b.IsActive)
                    .ToListAsync()
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
