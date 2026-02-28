using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireOwner]
    public class AuditLogController : BaseController
    {
        public AuditLogController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index(string? entityType, string? logAction, DateTime? from, DateTime? to, int page = 1)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);

            if (!string.IsNullOrEmpty(logAction))
                query = query.Where(a => a.Action == logAction);

            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.Timestamp < to.Value.AddDays(1));

            var totalItems = await query.CountAsync();
            var pageSize = 25;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var logs = await query
                .Include(a => a.Branch)
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get distinct values for filters
            ViewBag.EntityTypes = await _context.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(e => e).ToListAsync();
            ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
            ViewBag.SelectedEntityType = entityType;
            ViewBag.SelectedAction = logAction;
            ViewBag.FromDate = from;
            ViewBag.ToDate = to;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(logs);
        }

        public async Task<IActionResult> ExportCsv(string? entityType, string? logAction, DateTime? from, DateTime? to)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(logAction))
                query = query.Where(a => a.Action == logAction);
            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(a => a.Timestamp < to.Value.AddDays(1));

            var logs = await query.Include(a => a.Branch).OrderByDescending(a => a.Timestamp).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Action,EntityType,EntityId,UserName,UserRole,BranchId,BranchName,IpAddress,Timestamp,Details");
            foreach (var l in logs)
            {
                csv.AppendLine($"{l.Id},{EscapeCsv(l.Action)},{EscapeCsv(l.EntityType)},{l.EntityId?.ToString() ?? ""},{EscapeCsv(l.UserName ?? "")},{EscapeCsv(l.UserRole ?? "")},{l.BranchId?.ToString() ?? ""},{EscapeCsv(l.Branch?.Name ?? "")},{EscapeCsv(l.IpAddress ?? "")},{l.Timestamp:yyyy-MM-dd HH:mm:ss},{EscapeCsv(l.Details ?? "")}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"audit-log-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\""; 
            return value;
        }
    }
}
