using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class MenuReportService : IMenuReportService
    {
        private readonly ApplicationDbContext _context;

        public MenuReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MenuPerformanceViewModel> GenerateReportAsync(
            DateTime from, DateTime to,
            int? branchId, int? categoryId, int topN,
            string userRole, int? userBranchId)
        {
            var (effectiveBranchId, items) = await QueryItemPerformance(from, to, branchId, categoryId, userRole, userBranchId);

            // ---- category aggregation ----
            var totalRevenue = items.Sum(x => x.Revenue);
            var categories = items
                .GroupBy(i => new { i.CategoryName, i.CategoryColor })
                .Select(g =>
                {
                    var rev = g.Sum(x => x.Revenue);
                    return new CategoryPerformanceRow
                    {
                        CategoryName = g.Key.CategoryName,
                        Color = g.Key.CategoryColor,
                        ItemCount = g.Count(),
                        QuantitySold = g.Sum(x => x.QuantitySold),
                        Revenue = rev,
                        Profit = g.Sum(x => x.Profit),
                        AvgItemPrice = g.Any() ? g.Average(x => x.Price) : 0,
                        RevenueSharePct = totalRevenue > 0 ? rev / totalRevenue * 100 : 0
                    };
                })
                .OrderByDescending(c => c.Revenue)
                .ToList();

            // ---- branch name ----
            var currentBranch = effectiveBranchId.HasValue
                ? await _context.Branches.FindAsync(effectiveBranchId.Value)
                : null;

            // ---- top / least sellers ----
            var topSellers = items.OrderByDescending(x => x.QuantitySold).Take(topN).ToList();
            var leastSellers = items.OrderBy(x => x.QuantitySold).Take(topN).ToList();

            // ---- category name if filtered ----
            string? catName = null;
            if (categoryId.HasValue)
            {
                catName = (await _context.Categories.FindAsync(categoryId.Value))?.Name;
            }

            // ---- available filter options ----
            var availableBranches = await GetAccessibleBranches(userRole, userBranchId);
            var availableCategories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var totalProfit = items.Sum(x => x.Profit);

            return new MenuPerformanceViewModel
            {
                FromDate = from,
                ToDate = to,
                BranchId = effectiveBranchId,
                BranchName = currentBranch?.Name ?? "All Branches",
                CategoryId = categoryId,
                CategoryName = catName,
                TopN = topN,
                TotalItemsSold = items.Sum(x => x.QuantitySold),
                TotalMenuRevenue = totalRevenue,
                UniqueItemsOrdered = items.Count,
                AverageItemPrice = items.Any() ? items.Average(x => x.Price) : 0,
                TotalProfit = totalProfit,
                OverallProfitMargin = totalRevenue > 0 ? totalProfit / totalRevenue * 100 : 0,
                Items = items,
                Categories = categories,
                TopSellers = topSellers,
                LeastSellers = leastSellers,
                AvailableBranches = availableBranches,
                AvailableCategories = availableCategories
            };
        }

        public async Task<List<MenuItemPerformanceRow>> GetExportRowsAsync(
            DateTime from, DateTime to,
            int? branchId, int? categoryId,
            string userRole, int? userBranchId)
        {
            var (_, items) = await QueryItemPerformance(from, to, branchId, categoryId, userRole, userBranchId);
            return items;
        }

        // ================================================================
        //  Core query — single source of truth for both report & export
        // ================================================================
        private async Task<(int? effectiveBranchId, List<MenuItemPerformanceRow> items)> QueryItemPerformance(
            DateTime from, DateTime to,
            int? branchId, int? categoryId,
            string userRole, int? userBranchId)
        {
            // end date is exclusive (< end) so push one day forward if caller sends "to" inclusive
            var endExclusive = to.Date.AddDays(1);

            IQueryable<OrderItem> query = _context.OrderItems
                .Include(oi => oi.Order).ThenInclude(o => o.Branch)
                .Include(oi => oi.MenuItem).ThenInclude(m => m.Category);

            // Only completed orders
            query = query.Where(oi => oi.Order.Status == "Completed");

            // Date filter on Order.OrderDate
            query = query.Where(oi => oi.Order.OrderDate >= from && oi.Order.OrderDate < endExclusive);

            // ---- Role-based branch enforcement ----
            int? effectiveBranchId = branchId;
            if (userRole != "Owner")
            {
                // Manager gets locked to their branch; Staff should not reach this
                if (userBranchId.HasValue)
                {
                    effectiveBranchId = userBranchId.Value;
                    query = query.Where(oi => oi.Order.BranchId == userBranchId.Value);
                }
            }
            else if (branchId.HasValue)
            {
                query = query.Where(oi => oi.Order.BranchId == branchId.Value);
            }

            // ---- Category filter ----
            if (categoryId.HasValue)
            {
                query = query.Where(oi => oi.MenuItem.CategoryId == categoryId.Value);
            }

            // ---- Grouped projection (avoids N+1) ----
            var items = await query
                .GroupBy(oi => new
                {
                    oi.MenuItemId,
                    oi.MenuItem.Name,
                    CategoryName = oi.MenuItem.Category.Name,
                    CategoryColor = oi.MenuItem.Category.Color,
                    oi.MenuItem.Price,
                    oi.MenuItem.CostPrice,
                    oi.MenuItem.Availability,
                    BranchName = oi.Order.Branch.Name
                })
                .Select(g => new MenuItemPerformanceRow
                {
                    MenuItemId = g.Key.MenuItemId,
                    ItemName = g.Key.Name,
                    CategoryName = g.Key.CategoryName,
                    CategoryColor = g.Key.CategoryColor,
                    BranchName = g.Key.BranchName,
                    Price = g.Key.Price,
                    CostPrice = g.Key.CostPrice,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.Price),
                    OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
                    IsAvailable = g.Key.Availability
                })
                .OrderByDescending(x => x.Revenue)
                .ToListAsync();

            return (effectiveBranchId, items);
        }

        // ================================================================
        //  Branch access helper (mirrors BaseController logic)
        // ================================================================
        private async Task<List<Branch>> GetAccessibleBranches(string userRole, int? userBranchId)
        {
            if (userRole == "Owner")
                return await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();

            if (userBranchId.HasValue)
                return await _context.Branches.Where(b => b.Id == userBranchId.Value && b.IsActive).ToListAsync();

            return new List<Branch>();
        }
    }
}
