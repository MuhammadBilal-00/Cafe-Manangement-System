using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    public class FeedbackController : BaseController
    {
        public FeedbackController(ApplicationDbContext context) : base(context)
        {
        }

        // ADMIN: List + analytics
        public async Task<IActionResult> Index(
            int? rating,
            string status,
            int? branchId,
            DateTime? from,
            DateTime? to,
            string search,
            int page = 1,
            int pageSize = 20)
        {
            var query = _context.Feedbacks
                .Include(f => f.Customer)
                .Include(f => f.Branch)
                .AsQueryable();

            // Branch restriction by role
            var userRole = GetCurrentUserRole();
            if (userRole != "Owner")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                var staffBranchId = HttpContext.Session.GetStaffBranchId();

                if (managedBranchId.HasValue)
                    query = query.Where(f => f.BranchId == managedBranchId.Value);
                else if (staffBranchId.HasValue)
                    query = query.Where(f => f.BranchId == staffBranchId.Value);
            }
            else if (branchId.HasValue)
            {
                query = query.Where(f => f.BranchId == branchId.Value);
            }

            // Filters
            if (rating.HasValue)
                query = query.Where(f => f.Rating == rating.Value);

            if (!string.IsNullOrEmpty(status))
            {
                var s = status.ToLower();
                if (s == "open")
                    query = query.Where(f => f.Status == FeedbackStatus.Open);
                else if (s == "inprogress")
                    query = query.Where(f => f.Status == FeedbackStatus.InProgress);
                else if (s == "resolved")
                    query = query.Where(f => f.Status == FeedbackStatus.Resolved);
            }

            if (from.HasValue)
            {
                var start = from.Value.Date;
                query = query.Where(f => f.Date >= start);
            }

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(f => f.Date < end);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(f =>
                    (f.Comments ?? string.Empty).Contains(search) ||
                    (f.Customer != null && (
                        (f.Customer.Name ?? string.Empty).Contains(search) ||
                        (f.Customer.Email ?? string.Empty).Contains(search)
                    )));
            }

            // Paging
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var items = await query
                .OrderByDescending(f => f.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Stats
            var avgRating = await query.AverageAsync(f => (double?)f.Rating) ?? 0;
            var openCount = await query.CountAsync(f => f.Status == FeedbackStatus.Open);
            var resolvedCount = await query.CountAsync(f => f.Status == FeedbackStatus.Resolved);

            // Simple analytics
            var ratingDist = await query
                .GroupBy(f => f.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync();

            var byCategory = await query
                .GroupBy(f => f.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            var byBranch = await query
                .GroupBy(f => f.Branch)
                .Select(g => new
                {
                    Branch = g.Key,
                    Count = g.Count(),
                    AvgRating = g.Average(x => (double?)x.Rating) ?? 0
                })
                .ToListAsync();

            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();

            ViewBag.Rating = rating;
            ViewBag.Status = status;
            ViewBag.BranchId = branchId;
            ViewBag.Search = search;
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            ViewBag.AverageRating = avgRating;
            ViewBag.OpenCount = openCount;
            ViewBag.ResolvedCount = resolvedCount;
            ViewBag.TotalCount = totalCount;

            ViewBag.RatingDist = ratingDist;
            ViewBag.ByCategory = byCategory;
            ViewBag.ByBranch = byBranch;

            return View(items);
        }

        // ADMIN: Details + status
        public async Task<IActionResult> Details(int id)
        {
            var feedback = await _context.Feedbacks
                .Include(f => f.Customer)
                .Include(f => f.Branch)
                .Include(f => f.Order)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return NotFound();

            if (!CanAccessBranch(feedback.BranchId))
                return AccessDenied();

            return View(feedback);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, FeedbackStatus status, string? staffNote)
        {
            var feedback = await _context.Feedbacks
                .Include(f => f.Branch)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null || !CanAccessBranch(feedback.BranchId))
                return Json(new { success = false, message = "Feedback not found or access denied." });

            feedback.Status = status;
            if (!string.IsNullOrWhiteSpace(staffNote))
                feedback.StaffNote = staffNote;

            if (status == FeedbackStatus.Resolved)
                feedback.ResolvedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = feedback.Id });
        }

        // CUSTOMER / STAFF: Create (no auth for now)
        public IActionResult Create(int? branchId, int? orderId)
        {
            ViewBag.Branches = _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToList();
            ViewBag.BranchId = branchId;
            ViewBag.OrderId = orderId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int rating,
            string? comments,
            int branchId,
            string? category,
            string? source,
            int? orderId)
        {
            if (rating < 1 || rating > 5)
                ModelState.AddModelError(nameof(rating), "Rating must be between 1 and 5.");

            if (rating <= 2 && string.IsNullOrWhiteSpace(comments))
                ModelState.AddModelError(nameof(comments), "Please tell us what went wrong.");

            if (!ModelState.IsValid)
            {
                ViewBag.Branches = _context.Branches
                    .Where(b => b.IsActive)
                    .OrderBy(b => b.Name)
                    .ToList();
                ViewBag.BranchId = branchId;
                ViewBag.OrderId = orderId;
                return View();
            }

            var feedback = new Feedback
            {
                CustomerId = null,
                BranchId = branchId,
                Rating = rating,
                Comments = comments,
                Category = category,
                Source = string.IsNullOrWhiteSpace(source) ? "General" : source,
                OrderId = orderId,
                Date = DateTime.Now,
                Status = FeedbackStatus.Open
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return RedirectToAction("Thanks");
        }

        public IActionResult Thanks()
        {
            return View();
        }

        // ADMIN: Needs attention (low-rated, unresolved, last 7 days)
        public async Task<IActionResult> NeedsAttention()
        {
            var cutoff = DateTime.Now.AddDays(-7);

            var query = _context.Feedbacks
                .Include(f => f.Customer)
                .Include(f => f.Branch)
                .Where(f => f.Rating <= 2 && f.Status != FeedbackStatus.Resolved && f.Date >= cutoff);

            var userRole = GetCurrentUserRole();
            if (userRole != "Owner")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                var staffBranchId = HttpContext.Session.GetStaffBranchId();

                if (managedBranchId.HasValue)
                    query = query.Where(f => f.BranchId == managedBranchId.Value);
                else if (staffBranchId.HasValue)
                    query = query.Where(f => f.BranchId == staffBranchId.Value);
            }

            var list = await query
                .OrderBy(f => f.Date)
                .ToListAsync();

            return View(list);
        }

        public async Task<IActionResult> ExportCsv()
        {
            var query = _context.Feedbacks
                .Include(f => f.Customer)
                .Include(f => f.Branch)
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            if (userRole != "Owner")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                var staffBranchId = HttpContext.Session.GetStaffBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(f => f.BranchId == managedBranchId.Value);
                else if (staffBranchId.HasValue)
                    query = query.Where(f => f.BranchId == staffBranchId.Value);
            }

            var feedbacks = await query.OrderByDescending(f => f.Date).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Branch,Customer,Rating,Category,Source,Status,Date,Comments,StaffNote,ResolvedAt");
            foreach (var f in feedbacks)
            {
                csv.AppendLine($"{f.Id},{EscapeCsv(f.Branch?.Name ?? "")},{EscapeCsv(f.Customer?.Name ?? "Guest")},{f.Rating},{EscapeCsv(f.Category ?? "")},{EscapeCsv(f.Source ?? "")},{EscapeCsv(f.Status.ToString())},{f.Date:yyyy-MM-dd},{EscapeCsv(f.Comments ?? "")},{EscapeCsv(f.StaffNote ?? "")},{f.ResolvedAt?.ToString("yyyy-MM-dd") ?? ""}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"feedback-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"\"")}\""; 
            return value;
        }
    }
}