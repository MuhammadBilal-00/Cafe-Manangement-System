using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Operations setup: kitchen/bar printers (KOT) + category→station routing. Manager/Owner only.</summary>
    [RequireManagerOrOwner]
    public class KitchenPrinterController : BaseController
    {
        private readonly IKotPrintService _kot;
        private readonly IAuditLogService _audit;

        public KitchenPrinterController(ApplicationDbContext context, IKotPrintService kot, IAuditLogService audit) : base(context)
        {
            _kot = kot;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;

            var printers = effective.HasValue
                ? await _context.KitchenPrinters.Where(p => p.BranchId == effective.Value)
                    .OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToListAsync()
                : new List<KitchenPrinter>();

            // Category routing (station per category) + the set of stations printers serve.
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive)
                .OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.KotStation }).ToListAsync();
            ViewBag.Stations = printers.Select(p => p.Station).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            return View(printers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int branchId, string name, string connectionType,
            string? ipAddress, int port, string station, bool isDefault, bool isActive)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            connectionType = connectionType == "Browser" ? "Browser" : "Network";
            if (connectionType == "Network" && string.IsNullOrWhiteSpace(ipAddress))
                return Json(new { success = false, message = "Network printers need an IP address." });
            station = string.IsNullOrWhiteSpace(station) ? "Kitchen" : station.Trim();
            port = port <= 0 ? 9100 : port;

            KitchenPrinter printer;
            if (id == 0)
            {
                printer = new KitchenPrinter { BranchId = branchId };
                _context.KitchenPrinters.Add(printer);
            }
            else
            {
                printer = await _context.KitchenPrinters.FirstOrDefaultAsync(p => p.Id == id);
                if (printer == null || !CanAccessBranch(printer.BranchId)) return Json(new { success = false, message = "Not found." });
            }

            printer.Name = name.Trim();
            printer.ConnectionType = connectionType;
            printer.IpAddress = ipAddress?.Trim();
            printer.Port = port;
            printer.Station = station;
            printer.IsDefault = isDefault;
            printer.IsActive = isActive;
            await _context.SaveChangesAsync();

            // Only one default printer per branch.
            if (isDefault)
                await _context.KitchenPrinters
                    .Where(p => p.BranchId == branchId && p.Id != printer.Id && p.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));

            await _audit.LogAsync(id == 0 ? "Create" : "Update", "KitchenPrinter", printer.Id, $"{printer.Name} ({printer.Station})", branchId);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var printer = await _context.KitchenPrinters.FirstOrDefaultAsync(p => p.Id == id);
            if (printer == null || !CanAccessBranch(printer.BranchId)) return Json(new { success = false, message = "Not found." });
            _context.KitchenPrinters.Remove(printer);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Delete", "KitchenPrinter", id, printer.Name, printer.BranchId);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(int id)
        {
            var printer = await _context.KitchenPrinters.FirstOrDefaultAsync(p => p.Id == id);
            if (printer == null || !CanAccessBranch(printer.BranchId)) return Json(new { success = false, message = "Not found." });
            var r = await _kot.PrintTestAsync(id);
            return Json(new { success = r.Ok, message = r.Message, browserUrl = r.BrowserUrl });
        }

        /// <summary>Route a menu category's items to a station (matched against printer stations).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCategoryStation(int categoryId, string? station)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
            if (cat == null) return Json(new { success = false, message = "Category not found." });
            cat.KotStation = string.IsNullOrWhiteSpace(station) ? null : station.Trim();
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
