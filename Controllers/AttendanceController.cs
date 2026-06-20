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
        private readonly INotificationService _notificationService;

        public AttendanceController(ApplicationDbContext context, IAttendanceService attendanceService, INotificationService notificationService) : base(context)
        {
            _attendanceService = attendanceService;
            _notificationService = notificationService;
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

            // Today's snapshot for dashboard KPIs
            var todayQuery = _context.Attendances
                .Where(a => a.Date == DateTime.Today);
            var todayBranchId = branchId;
            if (userRole == "BranchManager")
                todayBranchId = HttpContext.Session.GetManagedBranchId();
            if (todayBranchId.HasValue)
                todayQuery = todayQuery.Where(a => a.BranchId == todayBranchId.Value);

            var todayRecords = await todayQuery.ToListAsync();
            var totalActiveStaff = await _context.Staff
                .Where(s => s.IsActive && (!todayBranchId.HasValue || s.BranchId == todayBranchId.Value))
                .CountAsync();

            var leaveStatuses = new[] { "Paid Leave", "Sick Leave", "Casual Leave", "Holiday" };

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
                TotalHalfDay = allRecords.Count(r => r.Status == "Half-Day"),
                TotalPaidLeave = allRecords.Count(r => r.Status == "Paid Leave"),
                TotalSickLeave = allRecords.Count(r => r.Status == "Sick Leave"),
                TotalCasualLeave = allRecords.Count(r => r.Status == "Casual Leave"),
                TotalHoliday = allRecords.Count(r => r.Status == "Holiday"),
                TotalWFH = allRecords.Count(r => r.Status == "Work From Home"),
                TotalOvertime = allRecords.Count(r => r.Status == "Overtime"),
                // Today's snapshot
                TodayTotalStaff = totalActiveStaff,
                TodayCheckedIn = todayRecords.Count(r => r.Status is "Present" or "Late" or "Work From Home" or "Overtime"),
                TodayLate = todayRecords.Count(r => r.Status == "Late"),
                TodayOnLeave = todayRecords.Count(r => leaveStatuses.Contains(r.Status)),
                TodayAbsent = Math.Max(0, totalActiveStaff - todayRecords.Count)
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

        // POST: Attendance/Mark (auto-calculates status from ClockIn/ClockOut)
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

            try
            {
                await _attendanceService.MarkAttendanceAsync(
                    model.StaffId, staff.BranchId, model.Date,
                    model.CheckInTime, model.CheckOutTime,
                    model.Notes, GetCurrentUserId(),
                    model.ManualStatus);

                // Notification: attendance marked
                await _notificationService.CreateNotificationAsync(
                    "Attendance Marked",
                    $"Attendance marked for staff #{model.StaffId} on {model.Date:MMM dd, yyyy}.",
                    "Info", NotificationCategory.Staff,
                    branchId: staff.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Attendance/Index",
                    icon: "fas fa-calendar-check");

                SetSuccessMessage("Attendance marked successfully!");
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                SetErrorMessage(ex.Message);
                ViewBag.Branches = await GetAccessibleBranches();
                ViewBag.StaffList = await GetAccessibleStaff();
                return View(model);
            }
        }

        // GET: Attendance/MarkSelf (Clock In)
        public async Task<IActionResult> MarkSelf()
        {
            var currentStaffId = await GetCurrentStaffId();
            if (!currentStaffId.HasValue)
            {
                SetErrorMessage("You are not registered as a staff member.");
                return RedirectToAction(nameof(Index));
            }

            // Check if already clocked in today
            var todayRecord = await _attendanceService.GetTodayAttendanceAsync(currentStaffId.Value);
            if (todayRecord != null)
            {
                if (todayRecord.CheckOutTime.HasValue)
                {
                    SetErrorMessage("You have already completed your attendance for today.");
                    return RedirectToAction(nameof(Index));
                }
                // Already clocked in but not out - redirect to clock out
                return RedirectToAction(nameof(ClockOut));
            }

            return View(new AttendanceMarkViewModel
            {
                StaffId = currentStaffId.Value,
                Date = DateTime.Today,
                CheckInTime = DateTime.Now.TimeOfDay
            });
        }

        // POST: Attendance/MarkSelf (Clock In - status auto-calculated)
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

            try
            {
                await _attendanceService.MarkAttendanceAsync(
                    model.StaffId, staff.BranchId, model.Date,
                    model.CheckInTime, null, // Only clock-in, no clock-out yet
                    model.Notes, GetCurrentUserId());

                SetSuccessMessage("Clock-in recorded! Status will be finalized when you clock out.");
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                SetErrorMessage(ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Attendance/ClockOut
        public async Task<IActionResult> ClockOut()
        {
            var currentStaffId = await GetCurrentStaffId();
            if (!currentStaffId.HasValue)
            {
                SetErrorMessage("You are not registered as a staff member.");
                return RedirectToAction(nameof(Index));
            }

            var todayRecord = await _attendanceService.GetTodayAttendanceAsync(currentStaffId.Value);
            if (todayRecord == null)
            {
                SetErrorMessage("You have not clocked in today. Please mark your attendance first.");
                return RedirectToAction(nameof(MarkSelf));
            }

            if (todayRecord.CheckOutTime.HasValue)
            {
                SetErrorMessage("You have already clocked out today.");
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ClockInTime = todayRecord.CheckInTime;
            ViewBag.StaffName = todayRecord.Staff?.User?.Name ?? "Staff";
            return View();
        }

        // POST: Attendance/ClockOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClockOut(TimeSpan? checkOutTime)
        {
            var currentStaffId = await GetCurrentStaffId();
            if (!currentStaffId.HasValue)
            {
                SetErrorMessage("You are not registered as a staff member.");
                return RedirectToAction(nameof(Index));
            }

            var clockOut = checkOutTime ?? DateTime.Now.TimeOfDay;
            var result = await _attendanceService.ClockOutAsync(currentStaffId.Value, DateTime.Today, clockOut);

            if (result == null)
            {
                SetErrorMessage("No clock-in record found for today.");
                return RedirectToAction(nameof(Index));
            }

            SetSuccessMessage($"Clock-out recorded! Status: {result.Status}, Hours: {result.TotalHours:F1}h" +
                (result.OvertimeHours > 0 ? $", Overtime: {result.OvertimeHours:F1}h" : ""));
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
                    Status = existing?.Status ?? "Auto",
                    ExistingStatus = existing?.Status,
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

                try
                {
                    await _attendanceService.MarkAttendanceAsync(
                        entry.StaffId, model.BranchId, model.Date,
                        entry.CheckInTime, entry.CheckOutTime,
                        entry.Notes, GetCurrentUserId(),
                        entry.ManualStatus);
                    marked++;
                }
                catch (InvalidOperationException)
                {
                    // Skip duplicates silently in bulk mode
                }
            }

            SetSuccessMessage($"Attendance marked for {marked} staff member(s)! Status auto-calculated.");

            // Notification: bulk attendance
            if (marked > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    "Bulk Attendance Marked",
                    $"Attendance marked for {marked} staff member(s) on {model.Date:MMM dd, yyyy}.",
                    "Info", NotificationCategory.Staff,
                    branchId: model.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Attendance/Index",
                    icon: "fas fa-calendar-check");
            }

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
            ViewBag.TotalHours = record.TotalHours;
            ViewBag.OvertimeHours = record.OvertimeHours;
            return View(model);
        }

        // POST: Attendance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireManagerOrOwner]
        public async Task<IActionResult> Edit(int id, AttendanceMarkViewModel model)
        {
            var result = await _attendanceService.UpdateAttendanceAsync(
                id, model.CheckInTime, model.CheckOutTime, model.Notes, model.ManualStatus);

            if (result == null)
            {
                SetErrorMessage("Failed to update attendance record.");
                return RedirectToAction(nameof(Index));
            }

            SetSuccessMessage($"Attendance updated! Status: {result.Status}, Hours: {result.TotalHours:F1}h");
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
            csv.AppendLine("Date,Staff,Branch,Status,CheckIn,CheckOut,TotalHours,OvertimeHours,LateMinutes,Notes");
            foreach (var r in records)
            {
                csv.AppendLine($"{r.Date:yyyy-MM-dd},{EscapeCsv(r.Staff?.User?.Name ?? "")},{EscapeCsv(r.Branch?.Name ?? "")},{r.Status},{r.CheckInTime?.ToString(@"hh\:mm") ?? ""},{r.CheckOutTime?.ToString(@"hh\:mm") ?? ""},{r.TotalHours:F2},{r.OvertimeHours:F2},{r.LateMinutes},{EscapeCsv(r.Notes ?? "")}");
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
                _context.Attendances.Remove(record);
                await _context.SaveChangesAsync();
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

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
