using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>Floor / table management (Phase 1). Visual floor map + status + CRUD.</summary>
    [RequireFeature("Tables")]
    [RequireStaffOrAbove]
    public class TableController : BaseController
    {
        private readonly ITableService _tables;
        private readonly IAuditLogService _audit;

        public TableController(ApplicationDbContext context, ITableService tables, IAuditLogService audit) : base(context)
        {
            _tables = tables;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? branchId)
        {
            var effective = GetEffectiveBranchId(branchId) ?? (await GetAccessibleBranches()).FirstOrDefault()?.Id;
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = effective;
            var tables = effective.HasValue ? await _tables.GetByBranchAsync(effective.Value) : new List<RestaurantTable>();
            return View(tables);
        }

        [HttpGet]
        public async Task<IActionResult> GetTables(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var tables = await _tables.GetByBranchAsync(branchId);
            return Json(tables.Select(t => new { t.Id, t.Name, t.Capacity, t.Zone, t.Status }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, string status)
        {
            var table = await _tables.GetAsync(id);
            if (table == null || !CanAccessBranch(table.BranchId)) return Json(new { success = false, message = "Not found." });
            var ok = await _tables.SetStatusAsync(id, status);
            if (ok) await _audit.LogAsync("StatusChange", "RestaurantTable", id, $"Table {table.Name} → {status}", table.BranchId);
            return Json(new { success = ok, status });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Create(int branchId, string name, int capacity, string? zone)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });

            var table = await _tables.CreateAsync(new RestaurantTable
            {
                BranchId = branchId, Name = name.Trim(),
                Capacity = Math.Clamp(capacity, 1, 100), Zone = zone?.Trim(), Status = "Available"
            });
            await _audit.LogAsync("Create", "RestaurantTable", table.Id, $"Table {table.Name} added", branchId);
            return Json(new { success = true, id = table.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Update(int id, string name, int capacity, string? zone, bool isActive)
        {
            var table = await _tables.GetAsync(id);
            if (table == null || !CanAccessBranch(table.BranchId)) return Json(new { success = false, message = "Not found." });
            table.Name = name?.Trim() ?? table.Name;
            table.Capacity = Math.Clamp(capacity, 1, 100);
            table.Zone = zone?.Trim();
            table.IsActive = isActive;
            var ok = await _tables.UpdateAsync(table);
            return Json(new { success = ok });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var table = await _tables.GetAsync(id);
            if (table == null || !CanAccessBranch(table.BranchId)) return Json(new { success = false, message = "Not found." });
            var ok = await _tables.DeactivateAsync(id);
            if (ok) await _audit.LogAsync("SoftDelete", "RestaurantTable", id, $"Table {table.Name} removed", table.BranchId);
            return Json(new { success = ok });
        }
    }
}
