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
    [RequireFeature("Payroll")]
    [RequireManagerOrOwner]
    public class SalaryController : BaseController
    {
        private readonly ISalaryService _salaryService;
        private readonly ISalaryPolicyService _policyService;
        private readonly ISalaryCalculationService _calcService;
        private readonly INotificationService _notificationService;

        public SalaryController(
            ApplicationDbContext context,
            ISalaryService salaryService,
            ISalaryPolicyService policyService,
            ISalaryCalculationService calcService,
            INotificationService notificationService) : base(context)
        {
            _salaryService = salaryService;
            _policyService = policyService;
            _calcService = calcService;
            _notificationService = notificationService;
        }

        // ================================================================
        //  INDEX
        // ================================================================

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
                FinalizedCount = allRecords.Count(r => r.Status == "Finalized"),
                ActivePolicy = await _policyService.GetActivePolicyAsync()
            };

            return View(vm);
        }

        // ================================================================
        //  GENERATE & PREVIEW
        // ================================================================

        // GET: Salary/Generate
        public async Task<IActionResult> Generate()
        {
            var vm = new SalaryGenerateViewModel
            {
                Branches = await GetAccessibleBranches(),
                ActivePolicy = await _policyService.GetActivePolicyAsync()
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

        // POST: Salary/Preview  dry-run preview (no DB write)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(SalaryGenerateViewModel model)
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

                var run = await _salaryService.PreviewMonthlySalariesAsync(
                    model.Year, model.Month, model.BranchId, GetCurrentUserId());

                model.PreviewRecords = run.Records;
                model.IsPreview = true;
                model.Branches = await GetAccessibleBranches();
                model.ActivePolicy = await _policyService.GetActivePolicyAsync();

                if (run.SkippedStaff.Count > 0)
                    SetErrorMessage($"No base salary configured for: {string.Join(", ", run.SkippedStaff)}. " +
                        "They are excluded from this run — set their salary on the staff profile first.");

                return View("Generate", model);
            }
            catch (InvalidOperationException ex)
            {
                SetErrorMessage(ex.Message);
                return RedirectToAction(nameof(Generate));
            }
        }

        // POST: Salary/Generate  commit preview to DB
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

                var run = await _salaryService.GenerateMonthlySalariesAsync(
                    model.Year, model.Month, model.BranchId, GetCurrentUserId());
                var results = run.Records;

                int newCount = results.Count(r => r.GeneratedAt.Date == DateTime.Today);

                // Notification: salaries generated
                await _notificationService.CreateNotificationAsync(
                    "Salaries Generated",
                    $"{results.Count} salary records generated for {model.Month}/{model.Year} ({newCount} new).",
                    "Success", NotificationCategory.Financial,
                    branchId: model.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: $"/Salary/Index?year={model.Year}&month={model.Month}",
                    icon: "fas fa-calculator");

                SetSuccessMessage($"Salary records generated successfully! {results.Count} total ({newCount} new). All records start as Draft.");
                if (run.SkippedStaff.Count > 0)
                    SetErrorMessage($"Skipped (no base salary configured): {string.Join(", ", run.SkippedStaff)}. " +
                        "Set their salary on the staff profile, then generate again for this month.");
            }
            catch (Exception ex)
            {
                SetErrorMessage($"Error generating salaries: {ex.Message}");
            }

            return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month, branchId = model.BranchId });
        }

        // ================================================================
        //  PAYSLIP
        // ================================================================

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
                Adjustments = adjustments,
                PolicyUsed = record.PolicyUsed
            };

            return View(vm);
        }

        // ================================================================
        //  WORKFLOW: Finalize, Unlock
        // ================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Finalize(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return AccessDenied();

            var success = await _salaryService.FinalizeSalaryAsync(id, userId.Value);
            if (success)
            {
                // Notification: salary finalized
                await _notificationService.CreateNotificationAsync(
                    "Salary Finalized",
                    $"Salary record #{id} has been finalized and is ready for payment.",
                    "Success", NotificationCategory.Financial,
                    roleTarget: "BranchManager",
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Salary/Index",
                    icon: "fas fa-check-double");

                SetSuccessMessage("Salary finalized! It can now be marked as paid.");
            }
            else
                SetErrorMessage("Failed to finalize salary. It may already be finalized or paid.");

            return RedirectToAction(nameof(Index));
        }

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

            // Notification: bulk finalize
            if (finalized > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    "Salaries Finalized",
                    $"{finalized} salary record(s) finalized for {month}/{year}.",
                    "Success", NotificationCategory.Financial,
                    branchId: branchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: $"/Salary/Index?year={year}&month={month}",
                    icon: "fas fa-check-double");
            }

            return RedirectToAction(nameof(Index), new { year, month, branchId });
        }

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
                SetErrorMessage("Cannot unlock. Record may be paid or not finalized.");

            return RedirectToAction(nameof(Index));
        }

        // ================================================================
        //  PAYMENT
        // ================================================================

        // GET: Salary/MarkPaid/5 — show payment details form
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

            if (record.Status != "Finalized")
            {
                SetErrorMessage("Salary must be Finalized before marking as paid.");
                return RedirectToAction(nameof(Index));
            }

            return View(record);
        }

        [HttpPost, ActionName("MarkPaid")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaidConfirm(int id, string paymentMethod, string? paymentReference, string? paymentNotes)
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
            {
                // Store payment details
                record.PaymentMethod    = paymentMethod;
                record.PaymentReference = paymentReference;
                record.PaymentNotes     = paymentNotes;
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationAsync(
                    "Salary Paid",
                    $"Salary for {record.Staff?.User?.Name ?? $"Staff #{record.StaffId}"} has been marked as paid via {paymentMethod}.",
                    "Success", NotificationCategory.Financial,
                    branchId: record.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Salary/Index",
                    icon: "fas fa-money-bill-check");

                SetSuccessMessage($"Salary marked as paid via {paymentMethod}!");
            }
            else
                SetErrorMessage("Failed to mark salary as paid. Salary must be finalized first.");

            return RedirectToAction(nameof(Index));
        }

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
                    && sr.Status == "Finalized");

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

        // ================================================================
        //  ADJUSTMENTS
        // ================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdjustment([FromBody] SalaryAdjustRequest request)
        {
            if (request == null || request.Amount <= 0)
                return Json(new { success = false, message = "Invalid request. Amount must be greater than 0." });

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdjustment(int id)
        {
            var adjustment = await _context.SalaryAdjustments
                .Include(a => a.SalaryRecord)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (adjustment == null)
                return Json(new { success = false, message = "Adjustment not found." });

            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue || adjustment.SalaryRecord.BranchId != managedBranchId.Value)
                    return Json(new { success = false, message = "Access denied." });
            }

            var success = await _salaryService.RemoveAdjustmentAsync(id);
            return Json(new { success, message = success ? "Adjustment removed." : "Cannot remove from a finalized/paid record." });
        }

        // ================================================================
        //  BASE SALARY MANAGEMENT
        // ================================================================

        [RequireOwner]
        public async Task<IActionResult> BaseSalaryHistory(int staffId)
        {
            var staff = await _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == staffId);

            if (staff == null) return NotFound();

            var history = await _salaryService.GetBaseSalaryHistoryAsync(staffId);
            var currentBase = await _calcService.GetEffectiveBaseSalaryAsync(
                staffId, DateTime.Now.Year, DateTime.Now.Month);

            var vm = new BaseSalaryHistoryViewModel
            {
                Staff = staff,
                History = history,
                CurrentBaseSalary = currentBase
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> UpdateBaseSalary([FromBody] BaseSalaryChangeRequest request)
        {
            if (request == null || request.NewBaseSalary <= 0)
                return Json(new { success = false, message = "Invalid base salary amount." });

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Json(new { success = false, message = "Not authenticated." });

            try
            {
                await _salaryService.UpdateBaseSalaryAsync(
                    request.StaffId, request.NewBaseSalary, userId.Value, request.Reason);

                return Json(new { success = true, message = $"Base salary updated to Rs. {request.NewBaseSalary:N0}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================================================================
        //  JSON API: Salary Detail
        // ================================================================

        [HttpGet]
        public async Task<IActionResult> GetSalaryDetail(int id)
        {
            var record = await _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Staff).ThenInclude(s => s.StaffRole)
                .Include(sr => sr.Branch)
                .Include(sr => sr.FinalizedBy)
                .Include(sr => sr.PolicyUsed)
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
                policyName = record.PolicyUsed?.Name ?? "Default",
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

        // ================================================================
        //  CSV EXPORT
        // ================================================================

        public async Task<IActionResult> ExportCsv(int? year, int? month, int? branchId)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            var query = _context.SalaryRecords
                .Include(sr => sr.Staff).ThenInclude(s => s.User)
                .Include(sr => sr.Branch)
                .Include(sr => sr.PolicyUsed)
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
            csv.AppendLine("Staff,Branch,Policy,BaseSalary,WorkingDays,Present,Absent,Late,HalfDay,Attendance%,OvertimeHours,OvertimePay,AttendanceBonus,AbsenceDeduction,HalfDayDeduction,LatePenalty,GrossSalary,TotalDeductions,NetSalary,Status,PaymentStatus,PaidDate");
            foreach (var r in records)
            {
                csv.AppendLine($"{EscapeCsv(r.Staff?.User?.Name ?? "")},{EscapeCsv(r.Branch?.Name ?? "")},{EscapeCsv(r.PolicyUsed?.Name ?? "Default")},{r.BaseSalary:F2},{r.TotalWorkingDays},{r.DaysPresent},{r.DaysAbsent},{r.DaysLate},{r.DaysHalfDay},{r.AttendancePercentage:F2},{r.OvertimeHours:F2},{r.OvertimePay:F2},{r.AttendanceBonus:F2},{r.AbsenceDeduction:F2},{r.HalfDayDeduction:F2},{r.LatePenaltyDeduction:F2},{r.GrossSalary:F2},{r.TotalDeductions:F2},{r.FinalSalary:F2},{r.Status},{r.PaymentStatus},{r.PaidDate?.ToString("yyyy-MM-dd") ?? ""}");
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
