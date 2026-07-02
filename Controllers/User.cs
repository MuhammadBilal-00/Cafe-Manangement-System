using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>
    /// Closed platform: the tenant Administrator (Owner) is the ONLY role that creates and
    /// manages accounts. All queries are tenant-scoped by the global query filter; a Platform
    /// Admin managing a tenant does so through impersonation. Customers are CRM data records
    /// and are managed in the CRM module, never here.
    /// </summary>
    public class UserController : BaseController
    {
        private const int PageSize = 10;
        private readonly IAuthService _authService;
        private readonly IAuditLogService _auditLog;

        public UserController(ApplicationDbContext context, IAuthService authService,
            IAuditLogService auditLog) : base(context)
        {
            _authService = authService;
            _auditLog = auditLog;
        }

        // ── Admin-only user management ─────────────────────────────────────────────

        [RequireOwner]
        public async Task<IActionResult> Index(string? role, string? q, bool showInactive = false, int page = 1)
        {
            var query = InternalUsers();

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role == role);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.Name.Contains(q) || u.Email.Contains(q) || u.Phone.Contains(q));
            if (!showInactive)
                query = query.Where(u => u.IsActive);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var users = await query
                .OrderBy(u => u.Name)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(u => new { u.Id, u.Name, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedDate })
                .ToListAsync();

            // Batch branch lookups for the page (no N+1): managed branches + staff branches.
            var ids = users.Select(u => u.Id).ToList();
            var managed = await _context.Branches
                .Where(b => b.ManagerId != null && ids.Contains(b.ManagerId.Value))
                .Select(b => new { UserId = b.ManagerId!.Value, b.Name })
                .ToListAsync();
            var staffed = await _context.Staff
                .Where(s => ids.Contains(s.UserId))
                .Select(s => new { s.UserId, BranchName = s.Branch.Name })
                .ToListAsync();
            var branchByUser = managed
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.First().Name);
            foreach (var s in staffed.GroupBy(x => x.UserId))
                branchByUser.TryAdd(s.Key, s.First().BranchName);

            var rows = users.Select(u => new UserRowViewModel
            {
                Id = u.Id, Name = u.Name, Email = u.Email, Phone = u.Phone,
                Role = u.Role, IsActive = u.IsActive, CreatedDate = u.CreatedDate,
                BranchName = branchByUser.TryGetValue(u.Id, out var bn) ? bn : null
            }).ToList();
            ViewBag.Roles = AppRoles.AssignableTenantRoles;
            ViewBag.SelectedRole = role;
            ViewBag.Query = q;
            ViewBag.ShowInactive = showInactive;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.CurrentUserId = GetCurrentUserId();
            return View(rows);
        }

        [RequireOwner]
        public async Task<IActionResult> Create()
        {
            await PopulateFormLookups();
            return View(new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!AppRoles.AssignableTenantRoles.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
            if (string.IsNullOrEmpty(model.Password))
                ModelState.AddModelError(nameof(model.Password), "An initial password is required.");
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                ModelState.AddModelError(nameof(model.Email), "Email already exists");

            if (!ModelState.IsValid)
            {
                await PopulateFormLookups();
                return View(model);
            }

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                Phone = model.Phone,
                Role = model.Role,
                IsActive = model.IsActive,
                PasswordHash = _authService.HashPassword(model.Password!),
                CreatedDate = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await ApplyBranchAssignmentAsync(user, model.BranchId);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("Create", "User", user.Id,
                $"Admin created user {user.Name} ({AppRoles.Label(user.Role)})");
            SetSuccessMessage($"User {user.Name} created. Share their credentials securely.");
            return RedirectToAction(nameof(Index));
        }

        [RequireOwner]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await InternalUsers().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var model = new UserFormViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                IsActive = user.IsActive,
                BranchId = await CurrentBranchAssignmentAsync(user)
            };
            await PopulateFormLookups();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (model.Id != id) return NotFound();
            var user = await InternalUsers().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            if (!AppRoles.AssignableTenantRoles.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != id))
                ModelState.AddModelError(nameof(model.Email), "Email already exists");
            // The admin cannot demote or deactivate themselves — prevents locking the tenant out.
            if (id == GetCurrentUserId() && (model.Role != AppRoles.Owner || !model.IsActive))
                ModelState.AddModelError("", "You cannot change your own role or deactivate your own account.");

            if (!ModelState.IsValid)
            {
                await PopulateFormLookups();
                return View(model);
            }

            var oldRole = user.Role;
            user.Name = model.Name;
            user.Email = model.Email;
            user.Phone = model.Phone;
            user.Role = model.Role;
            user.IsActive = model.IsActive;
            // Password intentionally untouched here — resets go through ResetPassword only.

            await ApplyBranchAssignmentAsync(user, model.BranchId, oldRole);
            await _context.SaveChangesAsync();

            if (oldRole != user.Role)
                await _auditLog.LogAsync("RoleChange", "User", user.Id,
                    $"Role changed from {AppRoles.Label(oldRole)} to {AppRoles.Label(user.Role)} for {user.Name}");
            else
                await _auditLog.LogAsync("Update", "User", user.Id, $"Admin updated user {user.Name}");

            SetSuccessMessage($"User {user.Name} updated. Role/branch changes apply at their next sign-in.");
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Deactivate/reactivate. Data is kept; inactive users cannot sign in.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await InternalUsers().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();
            if (id == GetCurrentUserId())
            {
                SetErrorMessage("You cannot deactivate your own account.");
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();
            await _auditLog.LogAsync(user.IsActive ? "Activate" : "Deactivate", "User", user.Id,
                $"Admin {(user.IsActive ? "activated" : "deactivated")} {user.Name}");
            SetSuccessMessage($"{user.Name} is now {(user.IsActive ? "active" : "deactivated")}.");
            return RedirectToAction(nameof(Index));
        }

        public record ResetPasswordRequest(int Id, string NewPassword);

        /// <summary>
        /// Admin sets a new password and communicates it out-of-band. Never a self-service
        /// email reset; the hash is never exposed in any view or response.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return Json(new { success = false, message = "Password must be at least 6 characters." });

            var user = await InternalUsers().FirstOrDefaultAsync(u => u.Id == request.Id);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            user.PasswordHash = _authService.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();
            await _auditLog.LogAsync("PasswordReset", "User", user.Id,
                $"Admin reset the password for {user.Name}");
            return Json(new { success = true, message = $"Password reset for {user.Name}. Share it securely." });
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>Internal (manageable) users only — customers and platform admins never appear here.</summary>
        private IQueryable<User> InternalUsers() =>
            _context.Users.Where(u => u.Role != AppRoles.Customer && u.Role != AppRoles.PlatformAdmin);

        private async Task PopulateFormLookups()
        {
            ViewBag.Roles = AppRoles.AssignableTenantRoles;
            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive).OrderBy(b => b.Name)
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToListAsync();
        }

        private async Task<int?> CurrentBranchAssignmentAsync(User user)
        {
            if (user.Role == AppRoles.BranchManager)
                return await _context.Branches
                    .Where(b => b.ManagerId == user.Id)
                    .Select(b => (int?)b.Id).FirstOrDefaultAsync();
            if (AppRoles.IsStaffLevel(user.Role))
                return await _context.Staff
                    .Where(s => s.UserId == user.Id)
                    .Select(s => (int?)s.BranchId).FirstOrDefaultAsync();
            return null;
        }

        /// <summary>
        /// Branch assignment follows the login flow's expectations: BranchManager via
        /// Branch.ManagerId, staff-level roles via their Staff record's BranchId.
        /// </summary>
        private async Task ApplyBranchAssignmentAsync(User user, int? branchId, string? oldRole = null)
        {
            // Leaving the BranchManager role (or moving branch): release any branch pointing at them.
            if (oldRole == AppRoles.BranchManager || user.Role == AppRoles.BranchManager)
            {
                var current = await _context.Branches.Where(b => b.ManagerId == user.Id).ToListAsync();
                foreach (var b in current.Where(b => user.Role != AppRoles.BranchManager || b.Id != branchId))
                    b.ManagerId = null;
            }

            if (user.Role == AppRoles.BranchManager && branchId.HasValue)
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId.Value);
                if (branch != null) branch.ManagerId = user.Id;
            }
            else if (AppRoles.IsStaffLevel(user.Role) && branchId.HasValue)
            {
                var staff = await _context.Staff.FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (staff != null)
                {
                    staff.BranchId = branchId.Value;
                }
                else
                {
                    // Minimal employment record so branch scoping works at login; HR fills in
                    // the rest (role, salary policy) via the Staff module.
                    var defaultRole = await _context.StaffRoles
                        .Where(r => r.IsActive).OrderBy(r => r.Id).FirstOrDefaultAsync();
                    if (defaultRole != null)
                    {
                        _context.Staff.Add(new Staff
                        {
                            UserId = user.Id,
                            StaffRoleId = defaultRole.Id,
                            BranchId = branchId.Value,
                            HireDate = DateTime.Now,
                            IsActive = true
                        });
                    }
                }
            }
        }

        // ── Self-service (any signed-in user): profile, settings, own password ───────

        public async Task<IActionResult> Profile()
        {
            var userIdInt = HttpContext.Session.GetInt32("UserId");
            if (!userIdInt.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userIdInt.Value);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            return View(user);
        }

        public async Task<IActionResult> Settings()
        {
            var userIdInt = HttpContext.Session.GetInt32("UserId");
            if (!userIdInt.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userIdInt.Value);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userIdInt = HttpContext.Session.GetInt32("UserId");
            if (!userIdInt.HasValue)
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(userIdInt.Value);
                TempData["Error"] = "Please fix the validation errors.";
                return View("Settings", user);
            }

            var currentUser = await _context.Users.FindAsync(userIdInt.Value);
            if (currentUser == null)
                return RedirectToAction("Login", "Auth");

            if (!_authService.VerifyPassword(model.CurrentPassword, currentUser.PasswordHash ?? string.Empty))
            {
                TempData["Error"] = "Current password is incorrect.";
                return View("Settings", currentUser);
            }

            currentUser.PasswordHash = _authService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Settings");
        }
    }
}
