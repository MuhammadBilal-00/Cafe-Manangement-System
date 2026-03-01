using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Helpers;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class ReportsController : BaseController
    {
        private readonly IMenuReportService _menuReportService;
        private readonly IAuditLogService _auditLogService;

        public ReportsController(
            ApplicationDbContext context,
            IMenuReportService menuReportService,
            IAuditLogService auditLogService) : base(context)
        {
            _menuReportService = menuReportService;
            _auditLogService = auditLogService;
        }

        // HUB: /Reports
        public async Task<IActionResult> Index()
        {
            var branches = await GetAccessibleBranches();
            ViewBag.Branches = branches;
            return View();
        }

        // MAIN: /Reports/SalesOverview
        public async Task<IActionResult> SalesOverview(DateTime? from, DateTime? to, int? branchId)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);

            IQueryable<Order> baseQuery = _context.Orders;

            // role + branch filter first
            baseQuery = ApplyRoleBasedBranchFilter(baseQuery, branchId, out int? effectiveBranchId);

            // includes
            var query = baseQuery
                .Include(o => o.Branch)
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem);

            // date range filter
            var filtered = query
                .Where(o => o.OrderDate >= start && o.OrderDate < end);

            var completed = filtered.Where(o => o.Status == "Completed");

            var totalRevenue = await completed.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
            var totalOrders = await completed.CountAsync();
            var averageOrderValue = totalOrders == 0 ? 0m : totalRevenue / totalOrders;

            // recent orders for table (limit)
            var ordersList = await filtered
                .OrderByDescending(o => o.OrderDate)
                .Take(150)
                .ToListAsync();

            var currentBranch = effectiveBranchId.HasValue
                ? await _context.Branches.FindAsync(effectiveBranchId.Value)
                : null;

            // ==== ANALYTICS: revenue by day ====
            var daily = await completed
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    Orders = g.Count()
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            // ==== ANALYTICS: status breakdown ====
            var statusBreakdown = await filtered
                .GroupBy(o => o.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Revenue = g.Where(o => o.Status == "Completed")
                               .Sum(o => (decimal?)o.TotalAmount) ?? 0m
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            // ==== ANALYTICS: top items ====
            var topItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.MenuItem)
                .ThenInclude(m => m.Category)
                .Where(oi => oi.Order.OrderDate >= start
                             && oi.Order.OrderDate < end
                             && oi.Order.Status == "Completed")
                // respect same branch filter
                .Where(oi => !effectiveBranchId.HasValue || oi.Order.BranchId == effectiveBranchId.Value)
                .GroupBy(oi => oi.MenuItem)
                .Select(g => new
                {
                    ItemName = g.Key.Name,
                    Category = g.Key.Category.Name,
                    Qty = g.Sum(oi => (int?)oi.Quantity) ?? 0,
                    Rev = g.Sum(oi => (decimal?)(oi.Quantity * oi.Price)) ?? 0m
                })
                .OrderByDescending(x => x.Rev)
                .Take(8)
                .ToListAsync();

            // ==== ANALYTICS: revenue by branch ====
            var branchRevenue = await filtered
                .GroupBy(o => o.Branch)
                .Select(g => new
                {
                    Branch = g.Key,
                    Revenue = g.Where(o => o.Status == "Completed")
                               .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    Orders = g.Count()
                })
                .OrderByDescending(x => x.Revenue)
                .ToListAsync();

            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.DailyRevenue = daily;
            ViewBag.StatusBreakdown = statusBreakdown;
            ViewBag.TopItems = topItems;
            ViewBag.BranchRevenue = branchRevenue;

            var model = new SalesReportViewModel
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                AverageOrderValue = averageOrderValue,
                FromDate = start,
                ToDate = end.AddDays(-1),
                BranchId = effectiveBranchId,
                BranchName = currentBranch?.Name ?? "All Branches",
                Orders = ordersList
            };

            return View(model);
        }

        // ===== helpers =====

        // Wide default so dummy data shows (last 12 months)
        private (DateTime start, DateTime end) GetDateRangeOrDefault(DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;

            if (!from.HasValue && !to.HasValue)
            {
                var start = today.AddMonths(-12);
                var end = today.AddDays(1);
                return (start, end);
            }

            var s = from?.Date ?? today.AddMonths(-12);
            var e = (to?.Date ?? today).AddDays(1);

            if (e <= s)
            {
                e = s.AddDays(1);
            }

            return (s, e);
        }

        private IQueryable<Order> ApplyRoleBasedBranchFilter(
            IQueryable<Order> query,
            int? branchId,
            out int? effectiveBranchId)
        {
            effectiveBranchId = branchId;

            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

                if (userBranchId.HasValue)
                {
                    effectiveBranchId = userBranchId.Value;
                    query = query.Where(o => o.BranchId == userBranchId.Value);
                }
            }
            else if (branchId.HasValue)
            {
                query = query.Where(o => o.BranchId == branchId.Value);
            }

            return query;
        }

        // CSV Export: /Reports/ExportSalesCsv
        public async Task<IActionResult> ExportSalesCsv(DateTime? from, DateTime? to, int? branchId)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);

            IQueryable<Order> baseQuery = _context.Orders;
            baseQuery = ApplyRoleBasedBranchFilter(baseQuery, branchId, out _);

            var orders = await baseQuery
                .Include(o => o.Branch)
                .Include(o => o.Customer)
                .Where(o => o.OrderDate >= start && o.OrderDate < end)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("OrderNumber,Date,Customer,Branch,Status,Amount");
            foreach (var o in orders)
            {
                csv.AppendLine($"{o.OrderNumber},{o.OrderDate:yyyy-MM-dd},{EscapeCsv(o.Customer?.Name ?? "")},{EscapeCsv(o.Branch?.Name ?? "")},{EscapeCsv(o.Status)},{o.TotalAmount:F2}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"sales-report-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
        // BRANCH PERFORMANCE: /Reports/BranchPerformance
        // BRANCH PERFORMANCE: /Reports/BranchPerformance
        public async Task<IActionResult> BranchPerformance(DateTime? from, DateTime? to)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);

            // base query over orders
            IQueryable<Order> baseQuery = _context.Orders
                .Include(o => o.Branch);

            // role-based filtering (no explicit branchId here, we want "what this user can see")
            baseQuery = ApplyRoleBasedBranchFilter(baseQuery, null, out _);

            var filtered = baseQuery
                .Where(o => o.OrderDate >= start && o.OrderDate < end);

            // group by branch (orders)
            var grouped = await filtered
                .GroupBy(o => o.Branch)
                .Select(g => new
                {
                    Branch = g.Key,
                    TotalOrders = g.Count(),
                    CompletedOrders = g.Count(o => o.Status == "Completed"),
                    Revenue = g.Where(o => o.Status == "Completed")
                               .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                    AvgOrderValue = g.Where(o => o.Status == "Completed")
                                     .Average(o => (decimal?)o.TotalAmount) ?? 0m
                })
                .OrderByDescending(x => x.Revenue)
                .ToListAsync();

            // NEW: feedback stats for the same date range
            // We do not apply role-based filtering here yet; if you want to,
            // you can reuse ApplyRoleBasedBranchFilter logic for Feedbacks.
            var feedbackStats = await _context.Feedbacks
                .Where(f => f.Date >= start && f.Date < end)
                .GroupBy(f => f.BranchId)
                .Select(g => new
                {
                    BranchId = g.Key,
                    AvgRating = g.Average(x => (double?)x.Rating) ?? 0.0,
                    OpenCount = g.Count(x => x.Status != FeedbackStatus.Resolved)
                })
                .ToListAsync();

            var model = new BranchPerformanceViewModel
            {
                FromDate = start,
                ToDate = end.AddDays(-1),
                Branches = grouped.Select(b =>
                {
                    var feedback = feedbackStats.FirstOrDefault(fs => fs.BranchId == b.Branch.Id);

                    return new BranchPerformanceRow
                    {
                        BranchId = b.Branch.Id,
                        BranchName = b.Branch.Name,
                        Location = b.Branch.Location,
                        TotalOrders = b.TotalOrders,
                        CompletedOrders = b.CompletedOrders,
                        Revenue = b.Revenue,
                        AverageOrderValue = b.AvgOrderValue,
                        AvgFeedbackRating = feedback != null ? feedback.AvgRating : 0.0,
                        OpenFeedbackCount = feedback != null ? feedback.OpenCount : 0
                    };
                }).ToList()
            };

            return View(model);
        }
        // INVENTORY OVERVIEW: /Reports/InventoryOverview
        public async Task<IActionResult> InventoryOverview(int? branchId)
        {
            // base query over inventory items
            IQueryable<InventoryItem> baseQuery = _context.InventoryItems
                .Include(i => i.Branch);

            // role-based filtering; reuse branch helper but with inventory
            // we'll create a small overload here:
            int? effectiveBranchId;
            baseQuery = ApplyRoleBasedBranchFilterForInventory(baseQuery, branchId, out effectiveBranchId);

            var items = await baseQuery.ToListAsync();

            var totalValue = items.Sum(i => i.Quantity * i.UnitPrice);
            var totalItems = items.Count;
            var lowStockTotal = items.Count(i => i.Quantity <= i.ReorderLevel && i.Quantity > 0);
            var outOfStockTotal = items.Count(i => i.Quantity == 0);

            var byBranch = items
                .GroupBy(i => i.Branch)
                .Select(g => new InventoryBranchSummaryRow
                {
                    BranchId = g.Key.Id,
                    BranchName = g.Key.Name,
                    Location = g.Key.Location,
                    ItemCount = g.Count(),
                    LowStockCount = g.Count(i => i.Quantity <= i.ReorderLevel && i.Quantity > 0),
                    OutOfStockCount = g.Count(i => i.Quantity == 0),
                    TotalValue = g.Sum(i => i.Quantity * i.UnitPrice)
                })
                .OrderByDescending(b => b.TotalValue)
                .ToList();

            ViewBag.Branches = await GetAccessibleBranches();

            var model = new InventoryOverviewViewModel
            {
                SelectedBranchId = effectiveBranchId,
                Branches = byBranch,
                TotalItems = totalItems,
                LowStockItems = lowStockTotal,
                OutOfStockItems = outOfStockTotal,
                TotalInventoryValue = totalValue
            };

            return View(model);
        }
        // Role-based branch filtering for InventoryItem queries
        private IQueryable<InventoryItem> ApplyRoleBasedBranchFilterForInventory(
            IQueryable<InventoryItem> query,
            int? branchId,
            out int? effectiveBranchId)
        {
            effectiveBranchId = branchId;

            if (!HttpContext.Session.IsOwner())
            {
                var userBranchId = HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

                if (userBranchId.HasValue)
                {
                    effectiveBranchId = userBranchId.Value;
                    query = query.Where(i => i.BranchId == userBranchId.Value);
                }
            }
            else if (branchId.HasValue)
            {
                query = query.Where(i => i.BranchId == branchId.Value);
            }

            return query;
        }

        // ==============================
        // MENU PERFORMANCE (service-based)
        // ==============================
        public async Task<IActionResult> MenuPerformance(DateTime? from, DateTime? to, int? branchId, int? categoryId, int? topN)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);
            int n = topN ?? 10;

            var userRole = HttpContext.Session.GetUserRole() ?? "";
            var userBranchId = HttpContext.Session.IsOwner() ? (int?)null
                : HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

            var model = await _menuReportService.GenerateReportAsync(
                start, end.AddDays(-1), branchId, categoryId, n, userRole, userBranchId);

            await _auditLogService.LogAsync("ViewReport", "MenuPerformance", null,
                $"Menu performance report viewed ({model.FromDate:yyyy-MM-dd} to {model.ToDate:yyyy-MM-dd}, Branch={model.BranchName})");

            return View(model);
        }

        // EXCEL EXPORT
        public async Task<IActionResult> ExportMenuPerformanceExcel(DateTime? from, DateTime? to, int? branchId, int? categoryId)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);

            var userRole = HttpContext.Session.GetUserRole() ?? "";
            var userBranchId = HttpContext.Session.IsOwner() ? (int?)null
                : HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

            var rows = await _menuReportService.GetExportRowsAsync(
                start, end.AddDays(-1), branchId, categoryId, userRole, userBranchId);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Menu Performance");

            // Header row
            var headers = new[] { "Item", "Category", "Branch", "Price", "Cost", "Qty Sold", "Revenue", "Profit", "Margin %", "Orders", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2c2c2c");
                ws.Cell(1, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            }

            int row = 2;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.ItemName;
                ws.Cell(row, 2).Value = r.CategoryName;
                ws.Cell(row, 3).Value = r.BranchName ?? "";
                ws.Cell(row, 4).Value = r.Price;
                ws.Cell(row, 5).Value = r.CostPrice;
                ws.Cell(row, 6).Value = r.QuantitySold;
                ws.Cell(row, 7).Value = r.Revenue;
                ws.Cell(row, 8).Value = r.Profit;
                ws.Cell(row, 9).Value = Math.Round(r.ProfitMargin, 1);
                ws.Cell(row, 10).Value = r.OrderCount;
                ws.Cell(row, 11).Value = r.IsAvailable ? "Active" : "Inactive";

                ws.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 8).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(row, 9).Style.NumberFormat.Format = "0.0\"%\"";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            await _auditLogService.LogAsync("ExportReport", "MenuPerformance", null,
                $"Menu performance exported to Excel ({rows.Count} items)");

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"menu-performance-{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx");
        }

        // CSV EXPORT
        public async Task<IActionResult> ExportMenuPerformanceCsv(DateTime? from, DateTime? to, int? branchId, int? categoryId)
        {
            var (start, end) = GetDateRangeOrDefault(from, to);

            var userRole = HttpContext.Session.GetUserRole() ?? "";
            var userBranchId = HttpContext.Session.IsOwner() ? (int?)null
                : HttpContext.Session.IsBranchManager()
                    ? HttpContext.Session.GetManagedBranchId()
                    : HttpContext.Session.GetStaffBranchId();

            var rows = await _menuReportService.GetExportRowsAsync(
                start, end.AddDays(-1), branchId, categoryId, userRole, userBranchId);

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Item,Category,Branch,Price,CostPrice,QtySold,Revenue,Profit,ProfitMargin%,Orders,Status");
            foreach (var r in rows)
            {
                var profit = r.Profit;
                var margin = r.ProfitMargin;
                csv.AppendLine($"{EscapeCsv(r.ItemName)},{EscapeCsv(r.CategoryName)},{EscapeCsv(r.BranchName ?? "")},{r.Price:F2},{r.CostPrice:F2},{r.QuantitySold},{r.Revenue:F2},{profit:F2},{margin:F1},{r.OrderCount},{(r.IsAvailable ? "Active" : "Inactive")}");
            }

            await _auditLogService.LogAsync("ExportReport", "MenuPerformance", null,
                $"Menu performance exported to CSV ({rows.Count} items)");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"menu-performance-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }
    }
}