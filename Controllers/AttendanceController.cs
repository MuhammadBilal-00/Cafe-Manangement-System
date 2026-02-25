using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class AttendanceController : BaseController
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IAuditLogService _auditLogService;

        public AttendanceController(ApplicationDbContext context, IAttendanceService attendanceService,
            IAuditLogService auditLogService) : base(context)
        {
            _attendanceService = attendanceService;
            _auditLogService = auditLogService;
        }

        // GET: Attendance
        public async Task<IActionResult> Index(int? branchId, int? staffId, DateTime? from, DateTime? to,
            string? status, int page = 1, int pageSize = 25)
        {
            var query = _context.Attendances
                .Include(a => a.Staff).ThenInclude(s => s.User)
                .Include(a => a.Staff).ThenInclude(s => s.StaffRole)
                .Include(a => a.Branch)
                .AsQueryable();

            // Role-based filtering
            var userRole = GetCurrentUserRole();
            if (userRole == "Staff")
            {
                var currentStaffId = await GetCurrentStaffId();
                if (currentStaffId.HasValue)
                    query = query.Where(a => a.StaffId == currentStaffId.Value);
            }
            else if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(a => a.BranchId == managedBranchId.Value);
            }

            // Apply filters
            if (branchId.HasValue)
                query = query.Where(a => a.BranchId == branchId.Value);
            if (staffId.HasValue)
                query = query.Where(a => a.StaffId == staffId.Value);
            if (from.HasValue)
                query = query.Where(a => a.Date >= from.Value.Date);
            if (to.HasValue)
                query = query.Where(a => a.Date <= to.Value.Date);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var records = await query
                .OrderByDescending(a => a.Date)
                .ThenBy(a => a.Staff.User.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allRecords = await query.ToListAsync();

            var vm = new AttendanceIndexViewModel
            {
                Records = records,
                Branches = await GetAccessibleBranches(),
                StaffList = await GetAccessibleStaff(),
                BranchId = branchId,
                StaffId = staffId,
                FromDate = from,
                ToDate = to,
                Status = status,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalPresent = allRecords.Count(r => r.Status == "Present"),
                TotalAbsent = allRecords.Count(r => r.Status == "Absent"),
                TotalLate = allRecords.Count(r => r.Status == "Late"),
                TotalHalfDay = allRecords.Count(r => r.Status == "Half-Day")
            };

            return View(vm);
        }

        // GET: Attendance/Mark
        [RequireManagerOrOwner]
        public async Task<IActionResult> Mark()
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.StaffList = await GetAccessibleStaff();
            return View(new AttendanceMarkViewModel());
        }

        // POST: Attendance/Mark
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Mark(AttendanceMarkViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Branches = await GetAccessibleBranches();
                ViewBag.StaffList = await GetAccessibleStaff();
                return View(model);
            }

            var staff = await _context.Staff.FindAsync(model.StaffId);
            if (staff == null)
            {
                SetErrorMessage("Staff member not found.");
                return RedirectToAction(nameof(Index));
            }

            var success = await _attendanceService.MarkAttendanceAsync(
                model.StaffId, staff.BranchId, model.Date, model.Status,
                model.CheckInTime, model.CheckOutTime, model.LateMinutes,
                model.Notes, GetCurrentUserId());

            if (!success)
            {
                SetErrorMessage("Attendance already marked for this staff member on this date.");
                ViewBag.Branches = await GetAccessibleBranches();
                ViewBag.StaffList = await GetAccessibleStaff();
                return View(model);
            }

            await _auditLogService.LogAsync("Create", "Attendance", null,
                $"Marked attendance for staff #{model.StaffId} on {model.Date:yyyy-MM-dd} as {model.Status}",
                staff.BranchId);

            SetSuccessMessage("Attendance marked successfully!");
            return RedirectToAction(nameof(Index));
        }

        // GET: Attendance/MarkSelf
        public async Task<IActionResult> MarkSelf()
        {
            var currentStaffId = await GetCurrentStaffId();
            if (!currentStaffId.HasValue)
            {
                SetErrorMessage("You are not registered as a staff member.");
                return RedirectToAction(nameof(Index));
            }

            var already = await _attendanceService.HasAttendanceAsync(currentStaffId.Value, DateTime.Today);
            if (already)
            {
                SetErrorMessage("You have already marked your attendance for today.");
                return RedirectToAction(nameof(Index));
            }

            return View(new AttendanceMarkViewModel
            {
                StaffId = currentStaffId.Value,
                Date = DateTime.Today,
                CheckInTime = DateTime.Now.TimeOfDay
            });
        }

        // POST: Attendance/MarkSelf
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSelf(AttendanceMarkViewModel model)
        {
            var currentStaffId = await GetCurrentStaffId();
            if (!currentStaffId.HasValue)
            {
                SetErrorMessage("You are not registered as a staff member.");
                return RedirectToAction(nameof(Index));
            }

            model.StaffId = currentStaffId.Value;
            model.Date = DateTime.Today;

            var staff = await _context.Staff.FindAsync(model.StaffId);
            if (staff == null)
            {
                SetErrorMessage("Staff record not found.");
                return RedirectToAction(nameof(Index));
            }

            var success = await _attendanceService.MarkAttendanceAsync(
                model.StaffId, staff.BranchId, model.Date, model.Status,
                model.CheckInTime, model.CheckOutTime, model.LateMinutes,
                model.Notes, GetCurrentUserId());

            if (!success)
            {
                SetErrorMessage("You have already marked your attendance for today.");
                return RedirectToAction(nameof(Index));
            }

            await _auditLogService.LogAsync("Create", "Attendance", null,
                $"Self-marked attendance as {model.Status}", staff.BranchId);

            SetSuccessMessage("Attendance marked successfully!");
            return RedirectToAction(nameof(Index));
        }

        // GET: Attendance/BulkMark
        [RequireManagerOrOwner]
        public async Task<IActionResult> BulkMark(int? branchId, DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;
            var branches = await GetAccessibleBranches();

            if (!branchId.HasValue && branches.Any())
                branchId = branches.First().Id;

            var staffList = await _context.Staff
                .Include(s => s.User)
                .Where(s => s.IsActive && (!branchId.HasValue || s.BranchId == branchId.Value))
                .OrderBy(s => s.User.Name)
                .ToListAsync();

            var entries = new List<BulkAttendanceEntry>();
            foreach (var s in staffList)
            {
                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StaffId == s.Id && a.Date == targetDate.Date);

                entries.Add(new BulkAttendanceEntry
                {
                    StaffId = s.Id,
                    StaffName = s.User?.Name ?? "Unknown",
                    Status = existing?.Status ?? "Present",
                    CheckInTime = existing?.CheckInTime,
                    CheckOutTime = existing?.CheckOutTime,
                    LateMinutes = existing?.LateMinutes ?? 0,
                    Notes = existing?.Notes,
                    AlreadyMarked = existing != null
                });
            }

            ViewBag.Branches = branches;
            return View(new BulkAttendanceViewModel
            {
                BranchId = branchId ?? 0,
                Date = targetDate,
                Entries = entries
            });
        }

        // POST: Attendance/BulkMark
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> BulkMark(BulkAttendanceViewModel model)
        {
            int marked = 0;
            foreach (var entry in model.Entries)
            {
                if (entry.AlreadyMarked) continue;

                var success = await _attendanceService.MarkAttendanceAsync(
                    entry.StaffId, model.BranchId, model.Date, entry.Status,
                    entry.CheckInTime, entry.CheckOutTime, entry.LateMinutes,
                    entry.Notes, GetCurrentUserId());

                if (success) marked++;
            }

            await _auditLogService.LogAsync("BulkCreate", "Attendance", null,
                $"Bulk marked attendance for {marked} staff on {model.Date:yyyy-MM-dd}",
                model.BranchId);

            SetSuccessMessage($"Attendance marked for {marked} staff member(s)!");
            return RedirectToAction(nameof(Index));
        }

        // GET: Attendance/Edit/5
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id)
        {
            var record = await _attendanceService.GetAttendanceAsync(id);
            if (record == null) return NotFound();

            var model = new AttendanceMarkViewModel
            {
                StaffId = record.StaffId,
                Date = record.Date,
                CheckInTime = record.CheckInTime,
                CheckOutTime = record.CheckOutTime,
                Status = record.Status,
                LateMinutes = record.LateMinutes,
                Notes = record.Notes
            };

            ViewBag.AttendanceId = id;
            ViewBag.StaffName = record.Staff?.User?.Name ?? "Unknown";
            return View(model);
        }

        // POST: Attendance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id, AttendanceMarkViewModel model)
        {
            var success = await _attendanceService.UpdateAttendanceAsync(
                id, model.Status, model.CheckInTime, model.CheckOutTime,
                model.LateMinutes, model.Notes);

            if (!success)
            {
                SetErrorMessage("Failed to update attendance record.");
                return RedirectToAction(nameof(Index));
            }

            await _auditLogService.LogAsync("Update", "Attendance", id,
                $"Updated attendance to {model.Status}");

            SetSuccessMessage("Attendance updated successfully!");
            return RedirectToAction(nameof(Index));
        }

        // GET: Attendance/Summary
        public async Task<IActionResult> Summary(int? branchId, int? year, int? month)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            // Role-based branch filtering
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                branchId = HttpContext.Session.GetManagedBranchId();
            }

            var summaries = await _attendanceService.GetMonthlySummaryAsync(targetYear, targetMonth, branchId);

            var vm = new AttendanceSummaryViewModel
            {
                Summaries = summaries,
                Branches = await GetAccessibleBranches(),
                BranchId = branchId,
                Year = targetYear,
                Month = targetMonth
            };

            return View(vm);
        }

        // CSV Export
        public async Task<IActionResult> ExportCsv(int? branchId, int? staffId, DateTime? from, DateTime? to, string? status)
        {
            var query = _context.Attendances
                .Include(a => a.Staff).ThenInclude(s => s.User)
                .Include(a => a.Branch)
                .AsQueryable();

            var userRole = GetCurrentUserRole();
            if (userRole == "Staff")
            {
                var currentStaffId = await GetCurrentStaffId();
                if (currentStaffId.HasValue)
                    query = query.Where(a => a.StaffId == currentStaffId.Value);
            }
            else if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(a => a.BranchId == managedBranchId.Value);
            }

            if (branchId.HasValue) query = query.Where(a => a.BranchId == branchId.Value);
            if (staffId.HasValue) query = query.Where(a => a.StaffId == staffId.Value);
            if (from.HasValue) query = query.Where(a => a.Date >= from.Value.Date);
            if (to.HasValue) query = query.Where(a => a.Date <= to.Value.Date);
            if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);

            var records = await query.OrderByDescending(a => a.Date).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Date,Staff,Branch,Status,CheckIn,CheckOut,LateMinutes,Notes");
            foreach (var r in records)
            {
                csv.AppendLine($"{r.Date:yyyy-MM-dd},{EscapeCsv(r.Staff?.User?.Name ?? "")},{EscapeCsv(r.Branch?.Name ?? "")},{r.Status},{r.CheckInTime?.ToString(@"hh\:mm") ?? ""},{r.CheckOutTime?.ToString(@"hh\:mm") ?? ""},{r.LateMinutes},{EscapeCsv(r.Notes ?? "")}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"attendance-{DateTime.Now:yyyyMMdd}.csv");
        }

        // DELETE: Attendance/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record != null)
            {
                var branchId = record.BranchId;
                _context.Attendances.Remove(record);
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync("Delete", "Attendance", id,
                    "Deleted attendance record", branchId);
                SetSuccessMessage("Attendance record deleted.");
            }
            return RedirectToAction(nameof(Index));
        }

        // Helpers
        private async Task<int?> GetCurrentStaffId()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return null;
            var staff = await _context.Staff.FirstOrDefaultAsync(s => s.UserId == userId.Value && s.IsActive);
            return staff?.Id;
        }

        private async Task<List<Branch>> GetAccessibleBranches()
        {
            var role = GetCurrentUserRole();
            if (role == "Owner")
                return await _context.Branches.Where(b => b.IsActive).ToListAsync();

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    return await _context.Branches.Where(b => b.Id == branchId.Value).ToListAsync();
            }

            if (role == "Staff")
            {
                var branchId = HttpContext.Session.GetStaffBranchId();
                if (branchId.HasValue)
                    return await _context.Branches.Where(b => b.Id == branchId.Value).ToListAsync();
            }

            return new List<Branch>();
        }

        private async Task<List<Staff>> GetAccessibleStaff()
        {
            var role = GetCurrentUserRole();
            var query = _context.Staff.Include(s => s.User).Where(s => s.IsActive);

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    query = query.Where(s => s.BranchId == branchId.Value);
            }
            else if (role == "Staff")
            {
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                    query = query.Where(s => s.UserId == userId.Value);
            }

            return await query.OrderBy(s => s.User.Name).ToListAsync();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
