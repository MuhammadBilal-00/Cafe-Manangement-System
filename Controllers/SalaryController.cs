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
        [RequireOwner]
        public async Task<IActionResult> Generate()
        {
            var vm = new SalaryGenerateViewModel
            {
                Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync()
            };
            return View(vm);
        }

        // POST: Salary/Generate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Generate(SalaryGenerateViewModel model)
        {
            try
            {
                var results = await _salaryService.GenerateMonthlySalariesAsync(
                    model.Year, model.Month, model.BranchId, GetCurrentUserId());

                int newCount = results.Count(r => r.GeneratedAt.Date == DateTime.Today);

                await _auditLogService.LogAsync("Generate", "SalaryRecord", null,
                    $"Generated {newCount} salary records for {model.Year}-{model.Month:D2}");

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
        [RequireOwner]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var success = await _salaryService.MarkAsPaidAsync(id);
            if (success)
            {
                await _auditLogService.LogAsync("Update", "SalaryRecord", id, "Marked salary as paid");
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
        [RequireOwner]
        public async Task<IActionResult> MarkAllPaid(int year, int month, int? branchId)
        {
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
                $"Marked {pending.Count} salaries as paid for {year}-{month:D2}");

            SetSuccessMessage($"{pending.Count} salary records marked as paid!");
            return RedirectToAction(nameof(Index), new { year, month, branchId });
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
                    return await _context.Branches.Where(b => b.Id == branchId.Value).ToListAsync();
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
