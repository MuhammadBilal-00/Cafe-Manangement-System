using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 7: leave management. Approving a request stamps attendance rows for the dates
    /// (using the leave type's status), so the existing salary engine deducts/pays accordingly.</summary>
    [RequireFeature("Payroll")]
    [RequireManagerOrOwner]
    public class LeaveController : BaseController
    {
        private readonly IAuditLogService _audit;
        public LeaveController(ApplicationDbContext context, IAuditLogService audit) : base(context)
        {
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Types = await _context.LeaveTypes.OrderBy(t => t.Name).ToListAsync();
            ViewBag.Requests = await _context.LeaveRequests.Include(r => r.Staff).ThenInclude(s => s!.User).Include(r => r.LeaveType)
                .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();
            ViewBag.Staff = await GetAccessibleStaff();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveType(int id, string name, int daysPerYear, bool isPaid, string attendanceStatus)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            var valid = new[] { "Paid Leave", "Sick Leave", "Casual Leave", "Holiday" };
            if (!valid.Contains(attendanceStatus)) attendanceStatus = "Paid Leave";
            if (id == 0) _context.LeaveTypes.Add(new LeaveType { Name = name.Trim(), DaysPerYear = daysPerYear, IsPaid = isPaid, AttendanceStatus = attendanceStatus });
            else
            {
                var t = await _context.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return Json(new { success = false, message = "Not found." });
                t.Name = name.Trim(); t.DaysPerYear = daysPerYear; t.IsPaid = isPaid; t.AttendanceStatus = attendanceStatus;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Request(int staffId, int leaveTypeId, DateTime fromDate, DateTime toDate, string? reason)
        {
            if (toDate < fromDate) return Json(new { success = false, message = "End date must be on/after the start date." });
            var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff == null || !CanAccessBranch(staff.BranchId)) return Json(new { success = false, message = "Staff not accessible." });

            var days = (toDate.Date - fromDate.Date).Days + 1;
            _context.LeaveRequests.Add(new LeaveRequest { StaffId = staffId, LeaveTypeId = leaveTypeId, FromDate = fromDate.Date, ToDate = toDate.Date, Days = days, Reason = reason, Status = "Pending" });
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var req = await _context.LeaveRequests.Include(r => r.LeaveType).Include(r => r.Staff).FirstOrDefaultAsync(r => r.Id == id);
            if (req == null || req.Staff == null || !CanAccessBranch(req.Staff.BranchId)) return Json(new { success = false, message = "Not found." });
            if (req.Status != "Pending") return Json(new { success = false, message = "Already resolved." });

            var status = req.LeaveType?.AttendanceStatus ?? "Paid Leave";
            // Stamp attendance for each day so the salary engine treats it correctly.
            for (var d = req.FromDate.Date; d <= req.ToDate.Date; d = d.AddDays(1))
            {
                var existing = await _context.Attendances.FirstOrDefaultAsync(a => a.StaffId == req.StaffId && a.Date == d);
                if (existing != null) existing.Status = status;
                else _context.Attendances.Add(new Attendance { StaffId = req.StaffId, BranchId = req.Staff.BranchId, Date = d, Status = status, TotalHours = 0, OvertimeHours = 0, LateMinutes = 0, CreatedAt = DateTime.Now, MarkedById = GetCurrentUserId() });
            }
            req.Status = "Approved"; req.ApprovedById = GetCurrentUserId();
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Approve", "LeaveRequest", id, $"{req.Days} days {status}", req.Staff.BranchId);
            return Json(new { success = true, message = $"Approved — {req.Days} day(s) marked as {status}." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var req = await _context.LeaveRequests.Include(r => r.Staff).FirstOrDefaultAsync(r => r.Id == id);
            if (req == null || req.Staff == null || !CanAccessBranch(req.Staff.BranchId) || req.Status != "Pending") return Json(new { success = false });
            req.Status = "Rejected"; req.ApprovedById = GetCurrentUserId();
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
