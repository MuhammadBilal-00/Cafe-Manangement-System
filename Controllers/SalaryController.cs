using System;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Models.Requests;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class SalaryController : BaseController
    {
        private readonly ISalaryService _salaryService;
        private readonly IAuditLogService _auditLogService;

        public SalaryController(ApplicationDbContext context, ISalaryService salaryService,
            IAuditLogService auditLogService) : base(context)
        {
            _salaryService = salaryService;
            _auditLogService = auditLogService;
        }

        // GET: Salary
        public async Task<IActionResult> Index(int? branchId, int? year, int? month,
            string? paymentStatus, int page = 1, int pageSize = 25)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            var query = _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Where(sr => sr.Year == targetYear && sr.Month == targetMonth);

            // Role-based filtering
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(sr => sr.BranchId == managedBranchId.Value);
            }

            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);
            if (!string.IsNullOrEmpty(paymentStatus))
                query = query.Where(sr => sr.PaymentStatus == paymentStatus);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var allRecords = await query.ToListAsync();
            var records = allRecords
                .OrderBy(sr => sr.Staff?.User?.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new SalaryIndexViewModel
            {
                Records = records,
                Branches = await GetAccessibleBranches(),
                BranchId = branchId,
                Year = targetYear,
                Month = targetMonth,
                PaymentStatus = paymentStatus,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalBaseSalary = allRecords.Sum(r => r.BaseSalary),
                TotalBonuses = allRecords.Sum(r => r.BonusAmount),
                TotalDeductions = allRecords.Sum(r => r.DeductionAmount),
                TotalFinalSalary = allRecords.Sum(r => r.FinalSalary),
                PendingCount = allRecords.Count(r => r.PaymentStatus == "Pending"),
                PaidCount = allRecords.Count(r => r.PaymentStatus == "Paid")
            };

            return View(vm);
        }

        // GET: Salary/Generate
        public async Task<IActionResult> Generate()
        {
            var vm = new SalaryGenerateViewModel
            {
                Branches = await GetAccessibleBranches()
            };

            // Force branch for Manager
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    vm.BranchId = managedBranchId.Value;
            }

            return View(vm);
        }

        // POST: Salary/Generate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(SalaryGenerateViewModel model)
        {
            try
            {
                // Enforce branch for Manager
                var userRole = GetCurrentUserRole();
                if (userRole == "BranchManager")
                {
                    var managedBranchId = HttpContext.Session.GetManagedBranchId();
                    if (!managedBranchId.HasValue)
                        return AccessDenied();
                    model.BranchId = managedBranchId.Value;
                }

                var results = await _salaryService.GenerateMonthlySalariesAsync(
                    model.Year, model.Month, model.BranchId, GetCurrentUserId());

                int newCount = results.Count(r => r.GeneratedAt.Date == DateTime.Today);

                await _auditLogService.LogAsync("Generate", "SalaryRecord", null,
                    $"Generated {newCount} salary records for {model.Year}-{model.Month:D2}" +
                    (model.BranchId.HasValue ? $" (Branch {model.BranchId})" : " (All branches)"),
                    model.BranchId);

                SetSuccessMessage($"Salary records generated successfully! {results.Count} total ({newCount} new).");
            }
            catch (Exception ex)
            {
                SetErrorMessage($"Error generating salaries: {ex.Message}");
            }

            return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month, branchId = model.BranchId });
        }

        // GET: Salary/Payslip/5
        public async Task<IActionResult> Payslip(int id)
        {
            var record = await _salaryService.GetSalaryRecordAsync(id);
            if (record == null) return NotFound();

            // Ensure branch access
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue && record.BranchId != managedBranchId.Value)
                    return AccessDenied();
            }

            var startDate = new DateTime(record.Year, record.Month, 1);
            var endDate = startDate.AddMonths(1);

            var attendanceDetails = await _context.Attendances
                .Where(a => a.StaffId == record.StaffId && a.Date >= startDate && a.Date < endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();

            var vm = new PayslipViewModel
            {
                Record = record,
                AttendanceDetails = attendanceDetails
            };

            return View(vm);
        }

        // POST: Salary/MarkPaid/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            // Branch isolation check
            var record = await _salaryService.GetSalaryRecordAsync(id);
            if (record == null) return NotFound();

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue || record.BranchId != managedBranchId.Value)
                    return AccessDenied();
            }

            var success = await _salaryService.MarkAsPaidAsync(id);
            if (success)
            {
                await _auditLogService.LogAsync("Update", "SalaryRecord", id,
                    $"Marked salary as paid for staff #{record.StaffId}", record.BranchId);
                SetSuccessMessage("Salary marked as paid!");
            }
            else
            {
                SetErrorMessage("Failed to mark salary as paid.");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Salary/MarkAllPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllPaid(int year, int month, int? branchId)
        {
            // Enforce branch for Manager
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue)
                    return AccessDenied();
                branchId = managedBranchId.Value;
            }

            var query = _context.SalaryRecords
                .Where(sr => sr.Year == year && sr.Month == month && sr.PaymentStatus == "Pending");

            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);

            var pending = await query.ToListAsync();
            foreach (var r in pending)
            {
                r.PaymentStatus = "Paid";
                r.PaidDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync("BulkUpdate", "SalaryRecord", null,
                $"Marked {pending.Count} salaries as paid for {year}-{month:D2}" +
                (branchId.HasValue ? $" (Branch {branchId})" : ""),
                branchId);

            SetSuccessMessage($"{pending.Count} salary records marked as paid!");
            return RedirectToAction(nameof(Index), new { year, month, branchId });
        }

        // GET: Salary/GetSalaryDetail/5 (JSON API for dynamic summary)
        [HttpGet]
        public async Task<IActionResult> GetSalaryDetail(int id)
        {
            var record = await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (record == null) return Json(new { success = false, message = "Record not found" });

            // Branch isolation
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue && record.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied" });
            }

            // Salary history for this employee
            var history = await _context.SalaryRecords
                .Where(sr => sr.StaffId == record.StaffId)
                .OrderByDescending(sr => sr.Year).ThenByDescending(sr => sr.Month)
                .Take(6)
                .Select(sr => new
                {
                    sr.Year,
                    sr.Month,
                    sr.BaseSalary,
                    sr.BonusAmount,
                    sr.DeductionAmount,
                    sr.FinalSalary,
                    sr.AttendancePercentage,
                    sr.PaymentStatus
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                staffName = record.Staff?.User?.Name ?? "Unknown",
                role = record.Staff?.StaffRole?.RoleName ?? "N/A",
                branch = record.Branch?.Name ?? "N/A",
                baseSalary = record.BaseSalary,
                bonusAmount = record.BonusAmount,
                bonusReason = record.BonusReason ?? "",
                deductionAmount = record.DeductionAmount,
                deductionReason = record.DeductionReason ?? "",
                finalSalary = record.FinalSalary,
                daysPresent = record.DaysPresent,
                totalWorkingDays = record.TotalWorkingDays,
                daysAbsent = record.DaysAbsent,
                daysLate = record.DaysLate,
                daysHalfDay = record.DaysHalfDay,
                attendancePercentage = record.AttendancePercentage,
                paymentStatus = record.PaymentStatus,
                notes = record.Notes ?? "",
                history
            });
        }

        // POST: Salary/AdjustSalary (manual adjustment)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustSalary([FromBody] SalaryAdjustRequest request)
        {
            if (request == null)
                return Json(new { success = false, message = "Invalid request" });

            var record = await _context.SalaryRecords.FindAsync(request.RecordId);
            if (record == null)
                return Json(new { success = false, message = "Salary record not found" });

            // Branch isolation
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue || record.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied" });
            }

            if (record.PaymentStatus == "Paid")
                return Json(new { success = false, message = "Cannot adjust a paid salary record" });

            // Apply adjustments
            if (request.BaseSalary.HasValue && request.BaseSalary.Value >= 0)
                record.BaseSalary = request.BaseSalary.Value;

            if (request.BonusAmount.HasValue && request.BonusAmount.Value >= 0)
                record.BonusAmount = request.BonusAmount.Value;

            if (!string.IsNullOrEmpty(request.BonusReason))
                record.BonusReason = request.BonusReason;

            if (request.DeductionAmount.HasValue && request.DeductionAmount.Value >= 0)
                record.DeductionAmount = request.DeductionAmount.Value;

            if (!string.IsNullOrEmpty(request.DeductionReason))
                record.DeductionReason = request.DeductionReason;

            // Recalculate final salary based on attendance
            decimal effectiveBase = record.BaseSalary;
            if (record.TotalWorkingDays > 0)
            {
                decimal perDayRate = record.BaseSalary / record.TotalWorkingDays;
                decimal effectiveDays = record.DaysPresent + (record.DaysHalfDay * 0.5m);
                effectiveBase = perDayRate * effectiveDays;
            }
            record.FinalSalary = effectiveBase + record.BonusAmount - record.DeductionAmount;

            if (!string.IsNullOrEmpty(request.Notes))
                record.Notes = request.Notes;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("AdjustSalary", "SalaryRecord", record.Id,
                $"Adjusted salary for staff #{record.StaffId}: Base={record.BaseSalary:N0}, Bonus={record.BonusAmount:N0}, Deduction={record.DeductionAmount:N0}, Final={record.FinalSalary:N0}",
                record.BranchId);

            return Json(new { success = true, finalSalary = record.FinalSalary });
        }

        // CSV Export
        public async Task<IActionResult> ExportCsv(int? year, int? month, int? branchId)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            var query = _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Branch)
                .Where(sr => sr.Year == targetYear && sr.Month == targetMonth);

            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(sr => sr.BranchId == managedBranchId.Value);
            }

            var records = await query.OrderBy(sr => sr.Staff.User.Name).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Staff,Branch,BaseSalary,WorkingDays,Present,Absent,Late,HalfDay,Attendance%,Bonus,Deduction,FinalSalary,PaymentStatus,PaidDate");
            foreach (var r in records)
            {
                csv.AppendLine($"{EscapeCsv(r.Staff?.User?.Name ?? "")},{EscapeCsv(r.Branch?.Name ?? "")},{r.BaseSalary:F2},{r.TotalWorkingDays},{r.DaysPresent},{r.DaysAbsent},{r.DaysLate},{r.DaysHalfDay},{r.AttendancePercentage:F2},{r.BonusAmount:F2},{r.DeductionAmount:F2},{r.FinalSalary:F2},{r.PaymentStatus},{r.PaidDate?.ToString("yyyy-MM-dd") ?? ""}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"salary-{targetYear}{targetMonth:D2}.csv");
        }

        // Helpers
        private async Task<System.Collections.Generic.List<Branch>> GetAccessibleBranches()
        {
            var role = GetCurrentUserRole();
            if (role == "Owner")
                return await _context.Branches.Where(b => b.IsActive).ToListAsync();

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    return await _context.Branches.Where(b => b.Id == branchId.Value && b.IsActive).ToListAsync();
            }

            return new System.Collections.Generic.List<Branch>();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
