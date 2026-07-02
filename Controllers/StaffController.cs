using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Attributes;
using Cafe.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cafe.Services;

namespace Cafe.Controllers
{
    [RequireManagerOrOwner]
    public class StaffController : BaseController
    {
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;

        public StaffController(ApplicationDbContext context, IAuthService authService, INotificationService notificationService) : base(context)
        {
            _authService = authService;
            _notificationService = notificationService;
        }

        // GET: Staff
        public async Task<IActionResult> Index()
        {
            var query = _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .AsQueryable();

            // Branch isolation for Manager
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(s => s.BranchId == managedBranchId.Value);
            }

            var staff = await query.ToListAsync();
            return View(staff);
        }

        // GET: Staff/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var staff = await _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (staff == null) return NotFound();

            // Branch isolation check
            if (!CanAccessBranch(staff.BranchId))
                return AccessDenied();

            return View(staff);
        }

        // GET: Staff/Create
        // Creating a staff member also creates a LOGIN account, so this is Administrator-only
        // (closed platform: nobody below the admin can create accounts).
        [RequireOwner]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();

            // Auto-default branch for Manager
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    ViewBag.LockedBranchId = managedBranchId.Value;
            }

            return View();
        }

        // POST: Staff/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Create(StaffCreateViewModel model)
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

            if (ModelState.IsValid)
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    await PopulateDropdowns();
                    if (userRole == "BranchManager")
                        ViewBag.LockedBranchId = model.BranchId;
                    return View(model);
                }

                // Create new User
                var user = new User
                {
                    Name = model.Name,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = "Staff", // Set role to Staff automatically
                    PasswordHash = _authService.HashPassword(model.Password),
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create new Staff record
                var staff = new Staff
                {
                    UserId = user.Id,
                    StaffRoleId = model.StaffRoleId,
                    BranchId = model.BranchId,
                    EmploymentType = model.EmploymentType,
                    Department = model.Department,
                    DepartmentId = model.DepartmentId,
                    DesignationId = model.DesignationId,
                    EmployeeId = model.EmployeeId,
                    HireDate = DateTime.Now,
                    IsActive = model.IsActive
                };

                _context.Staff.Add(staff);
                await _context.SaveChangesAsync();

                // Notification: new staff added
                await _notificationService.CreateNotificationAsync(
                    "New Staff Member",
                    $"{model.Name} has been added as staff in branch #{model.BranchId}.",
                    "Success", NotificationCategory.Staff,
                    branchId: model.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Staff/Index",
                    icon: "fas fa-user-plus");

                TempData["Success"] = "Staff member created successfully!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns();
            if (GetCurrentUserRole() == "BranchManager")
            {
                var lockedBranch = HttpContext.Session.GetManagedBranchId();
                if (lockedBranch.HasValue) ViewBag.LockedBranchId = lockedBranch.Value;
            }
            return View(model);
        }

        // GET: Staff/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var staff = await _context.Staff
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (staff == null) return NotFound();

            // Branch isolation check
            if (!CanAccessBranch(staff.BranchId))
                return AccessDenied();

            var model = new StaffEditViewModel
            {
                Id = staff.Id,
                StaffRoleId = staff.StaffRoleId,
                BranchId = staff.BranchId,
                EmploymentType = staff.EmploymentType,
                Department = staff.Department,
                DepartmentId = staff.DepartmentId,
                DesignationId = staff.DesignationId,
                EmployeeId = staff.EmployeeId,
                PerformanceRating = staff.PerformanceRating,
                IsActive = staff.IsActive,
                Name = staff.User.Name,
                Email = staff.User.Email,
                Phone = staff.User.Phone
            };

            await PopulateDropdowns();

            // Lock branch for Manager
            var editRole = GetCurrentUserRole();
            if (editRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    ViewBag.LockedBranchId = managedBranchId.Value;
            }

            return View(model);
        }

        // POST: Staff/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaffEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Enforce branch for Manager
            var editUserRole = GetCurrentUserRole();
            if (editUserRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (!managedBranchId.HasValue)
                    return AccessDenied();
                model.BranchId = managedBranchId.Value;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var staff = await _context.Staff
                        .Include(s => s.User)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (staff == null)
                    {
                        return NotFound();
                    }

                    // Update User details
                    staff.User.Name = model.Name;
                    staff.User.Email = model.Email;
                    staff.User.Phone = model.Phone;

                    // Update Staff details
                    staff.StaffRoleId = model.StaffRoleId;
                    staff.BranchId = model.BranchId;
                    staff.EmploymentType = model.EmploymentType;
                    staff.Department = model.Department;
                    staff.DepartmentId = model.DepartmentId;
                    staff.DesignationId = model.DesignationId;
                    staff.EmployeeId = model.EmployeeId;
                    staff.PerformanceRating = model.PerformanceRating;
                    staff.IsActive = model.IsActive;

                    _context.Update(staff);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Staff member updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StaffExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns();
            if (GetCurrentUserRole() == "BranchManager")
            {
                var lockedBranch = HttpContext.Session.GetManagedBranchId();
                if (lockedBranch.HasValue) ViewBag.LockedBranchId = lockedBranch.Value;
            }
            return View(model);
        }

        // GET: Staff/Delete/5
        [RequireOwner]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var staff = await _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (staff == null)
            {
                return NotFound();
            }

            return View(staff);
        }

        // POST: Staff/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _context.Staff
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (staff != null)
            {
                // Soft delete (set IsActive to false)
                staff.IsActive = false;
                staff.TerminationDate = DateTime.Now;

                _context.Staff.Update(staff);
                await _context.SaveChangesAsync();

                // Notification: staff terminated
                await _notificationService.CreateNotificationAsync(
                    "Staff Member Removed",
                    $"{staff.User?.Name ?? "Staff"} has been deactivated.",
                    "Warning", NotificationCategory.Staff,
                    branchId: staff.BranchId,
                    createdBy: GetCurrentUserId(),
                    redirectUrl: "/Staff/Index",
                    icon: "fas fa-user-minus");

                TempData["Success"] = "Staff member deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool StaffExists(int id)
        {
            return _context.Staff.Any(e => e.Id == id);
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.StaffRoles = await _context.StaffRoles
                .Where(r => r.IsActive)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.RoleName
                })
                .ToListAsync();

            ViewBag.Branches = await GetBranchSelectList();

            // Phase 7/10: org-chart pickers for the staff form.
            ViewBag.Departments = await _context.Departments.Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
                .ToListAsync();
            ViewBag.Designations = await _context.Designations.Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
                .ToListAsync();
        }

        public async Task<IActionResult> ExportCsv()
        {
            var query = _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                .AsQueryable();

            // Branch isolation for Manager
            var userRole = GetCurrentUserRole();
            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                if (managedBranchId.HasValue)
                    query = query.Where(s => s.BranchId == managedBranchId.Value);
            }

            var staff = await query.OrderBy(s => s.User.Name).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("EmployeeId,Name,Email,Role,Branch,Department,EmploymentType,EmploymentStatus,HireDate,TerminationDate,PerformanceRating,IsActive");
            foreach (var s in staff)
            {
                csv.AppendLine($"{EscapeCsv(s.EmployeeId ?? "")},{EscapeCsv(s.User?.Name ?? "")},{EscapeCsv(s.User?.Email ?? "")},{EscapeCsv(s.StaffRole?.RoleName ?? "")},{EscapeCsv(s.Branch?.Name ?? "")},{EscapeCsv(s.Department ?? "")},{EscapeCsv(s.EmploymentType)},{EscapeCsv(s.EmploymentStatus)},{s.HireDate:yyyy-MM-dd},{s.TerminationDate?.ToString("yyyy-MM-dd") ?? ""},{s.PerformanceRating?.ToString() ?? ""},{s.IsActive}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"staff-{DateTime.Now:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"\"")}\""; 
            return value;
        }
    }
}