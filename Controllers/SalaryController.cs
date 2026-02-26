using System;
using System.Collections.Generic;
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

        public SalaryController(ApplicationDbContext context, ISalaryService salaryService) : base(context)
        {
            _salaryService = salaryService;
        }

        // GET: Salary
        public async Task<IActionResult> Index(int? branchId, int? year, int? month,
            string? paymentStatus, string? workflowStatus, int page = 1, int pageSize = 25)
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
            if (!string.IsNullOrEmpty(workflowStatus))
                query = query.Where(sr => sr.Status == workflowStatus);

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
                WorkflowStatus = workflowStatus,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalBaseSalary = allRecords.Sum(r => r.BaseSalary),
                TotalBonuses = allRecords.Sum(r => r.BonusAmount),
                TotalDeductions = allRecords.Sum(r => r.TotalDeductions),
                TotalFinalSalary = allRecords.Sum(r => r.FinalSalary),
                TotalOvertimePay = allRecords.Sum(r => r.OvertimePay),
                TotalAttendanceBonus = allRecords.Sum(r => r.AttendanceBonus),
                PendingCount = allRecords.Count(r => r.PaymentStatus == "Pending"),
                PaidCount = allRecords.Count(r => r.PaymentStatus == "Paid"),
                DraftCount = allRecords.Count(r => r.Status == "Draft"),
                FinalizedCount = allRecords.Count(r => r.Status == "Finalized")
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
                var userRole = GetCurrentUserRole();
                if (userRole == "BranchManager")
                {
                    var managedBranchId = HttpContext.Session.GetManagedBranchId();
                    if (!managedBranchId.HasValue) return AccessDenied();
                    model.BranchId = managedBranchId.Value;
                }

                var results = await _salaryService.GenerateMonthlySalariesAsync(
                    model.Year, model.Month, model.BranchId, GetCurrentUserId());

                int newCount = results.Count(r => r.GeneratedAt.Date == DateTime.Today);
                SetSuccessMessage($"Salary records generated successfully! {results.Count} total ({newCount} new). All records start as Draft.");
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

            var adjustments = await _context.SalaryAdjustments
                .Include(a => a.CreatedBy)
                .Where(a => a.SalaryRecordId == record.Id)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            var vm = new PayslipViewModel
            {
                Record = record,
                AttendanceDetails = attendanceDetails,
                Adjustments = adjustments
            };

            return View(vm);
        }

        // 
        //  WORKFLOW: Finalize & Unlock
        // 

        // POST: Salary/Finalize/5 (Owner only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Finalize(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var success = await _salaryService.FinalizeSalaryAsync(id, userId.Value);
            if (success)
                SetSuccessMessage("Salary finalized! It can now be marked as paid.");
            else
                SetErrorMessage("Failed to finalize salary. It may already be finalized.");

            return RedirectToAction(nameof(Index));
        }

        // POST: Salary/FinalizeAll (Owner only - finalize all Draft records for period)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> FinalizeAll(int year, int month, int? branchId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var query = _context.SalaryRecords
                .Where(sr => sr.Year == year && sr.Month == month && sr.Status == "Draft");

            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);

            var drafts = await query.ToListAsync();
            int finalized = 0;

            foreach (var record in drafts)
            {
                var success = await _salaryService.FinalizeSalaryAsync(record.Id, userId.Value);
                if (success) finalized++;
            }

            SetSuccessMessage($"{finalized} salary record(s) finalized!");
            return RedirectToAction(nameof(Index), new { year, month, branchId });
        }

        // POST: Salary/Unlock/5 (Owner only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Unlock(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var success = await _salaryService.UnlockSalaryAsync(id, userId.Value);
            if (success)
                SetSuccessMessage("Salary unlocked for editing.");
            else
                SetErrorMessage("Failed to unlock salary.");

            return RedirectToAction(nameof(Index));
        }

        // 
        //  PAYMENT
        // 

        // POST: Salary/MarkPaid/5 (requires Finalized)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
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
                SetSuccessMessage("Salary marked as paid!");
            else
                SetErrorMessage("Failed to mark salary as paid. Salary must be finalized first.");

            return RedirectToAction(nameof(Index));
        }

        // POST: Salary/MarkAllPaid (only Finalized + Pending)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllPaid(int year, int month, int? branchId)
        {
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue) return AccessDenied();
                branchId = managedBranchId.Value;
            }

            var query = _context.SalaryRecords
                .Where(sr => sr.Year == year && sr.Month == month
                    && sr.PaymentStatus == "Pending"
                    && sr.Status == "Finalized"); // Must be finalized

            if (branchId.HasValue)
                query = query.Where(sr => sr.BranchId == branchId.Value);

            var pending = await query.ToListAsync();
            int paid = 0;
            foreach (var r in pending)
            {
                var success = await _salaryService.MarkAsPaidAsync(r.Id);
                if (success) paid++;
            }

            SetSuccessMessage($"{paid} salary records marked as paid!");
            return RedirectToAction(nameof(Index), new { year, month, branchId });
        }

        // 
        //  ADJUSTMENTS (SalaryAdjustment table)
        // 

        // POST: Salary/AddAdjustment (JSON API)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdjustment([FromBody] SalaryAdjustRequest request)
        {
            if (request == null || request.Amount <= 0)
                return Json(new { success = false, message = "Invalid request. Amount must be greater than 0." });

            // Branch isolation check
            var record = await _salaryService.GetSalaryRecordAsync(request.RecordId);
            if (record == null)
                return Json(new { success = false, message = "Salary record not found." });

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue || record.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied." });
            }

            try
            {
                var adjustment = await _salaryService.AddAdjustmentAsync(
                    request.RecordId, request.Type, request.Amount,
                    request.Reason, GetCurrentUserId());

                return Json(new
                {
                    success = true,
                    adjustmentId = adjustment.Id,
                    finalSalary = record.FinalSalary,
                    message = $"{request.Type} of Rs. {request.Amount:N0} added successfully."
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Salary/RemoveAdjustment/5 (JSON API)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdjustment(int id)
        {
            var adjustment = await _context.SalaryAdjustments
                .Include(a => a.SalaryRecord)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (adjustment == null)
                return Json(new { success = false, message = "Adjustment not found." });

            // Branch isolation
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue || adjustment.SalaryRecord.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied." });
            }

            var success = await _salaryService.RemoveAdjustmentAsync(id);
            return Json(new { success, message = success ? "Adjustment removed." : "Cannot remove from a finalized record." });
        }

        // 
        //  JSON API: Salary Detail
        // 

        [HttpGet]
        public async Task<IActionResult> GetSalaryDetail(int id)
        {
            var record = await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Include(sr => sr.FinalizedBy)
                .Include(sr => sr.Adjustments).ThenInclude(a => a.CreatedBy)
                .FirstOrDefaultAsync(sr => sr.Id == id);

            if (record == null) return Json(new { success = false, message = "Record not found" });

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue && record.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied" });
            }

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
                    sr.PaymentStatus,
                    sr.Status
                })
                .ToListAsync();

            var adjustments = record.Adjustments?.Select(a => new
            {
                a.Id,
                a.Type,
                a.Amount,
                reason = a.Reason ?? "",
                createdBy = a.CreatedBy?.Name ?? "System",
                createdAt = a.CreatedAt.ToString("dd MMM yyyy HH:mm")
            }).ToList();

            return Json(new
            {
                success = true,
                staffName = record.Staff?.User?.Name ?? "Unknown",
                role = record.Staff?.StaffRole?.RoleName ?? "N/A",
                branch = record.Branch?.Name ?? "N/A",
                baseSalary = record.BaseSalary,
                overtimeHours = record.OvertimeHours,
                overtimePay = record.OvertimePay,
                attendanceBonus = record.AttendanceBonus,
                absenceDeduction = record.AbsenceDeduction,
                halfDayDeduction = record.HalfDayDeduction,
                latePenaltyDeduction = record.LatePenaltyDeduction,
                grossSalary = record.GrossSalary,
                totalDeductions = record.TotalDeductions,
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
                status = record.Status,
                paymentStatus = record.PaymentStatus,
                finalizedBy = record.FinalizedBy?.Name,
                finalizedAt = record.FinalizedAt?.ToString("dd MMM yyyy HH:mm"),
                notes = record.Notes ?? "",
                adjustments,
                history
            });
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
            csv.AppendLine("Staff,Branch,BaseSalary,WorkingDays,Present,Absent,Late,HalfDay,Attendance%,OvertimeHours,OvertimePay,AttendanceBonus,AbsenceDeduction,HalfDayDeduction,LatePenalty,GrossSalary,TotalDeductions,NetSalary,Status,PaymentStatus,PaidDate");
            foreach (var r in records)
            {
                csv.AppendLine($"{EscapeCsv(r.Staff?.User?.Name ?? "")},{EscapeCsv(r.Branch?.Name ?? "")},{r.BaseSalary:F2},{r.TotalWorkingDays},{r.DaysPresent},{r.DaysAbsent},{r.DaysLate},{r.DaysHalfDay},{r.AttendancePercentage:F2},{r.OvertimeHours:F2},{r.OvertimePay:F2},{r.AttendanceBonus:F2},{r.AbsenceDeduction:F2},{r.HalfDayDeduction:F2},{r.LatePenaltyDeduction:F2},{r.GrossSalary:F2},{r.TotalDeductions:F2},{r.FinalSalary:F2},{r.Status},{r.PaymentStatus},{r.PaidDate?.ToString("yyyy-MM-dd") ?? ""}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"salary-{targetYear}{targetMonth:D2}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
