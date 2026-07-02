// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;

namespace Cafe.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly IAuditLogService _auditLogService;
        private readonly ITenantContext _tenantContext;

        public AuthController(ApplicationDbContext context, IAuthService authService,
            IAuditLogService auditLogService, ITenantContext tenantContext)
        {
            _context = context;
            _authService = authService;
            _auditLogService = auditLogService;
            _tenantContext = tenantContext;
        }

        // GET: /Auth/Login
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserId").HasValue)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Login is inherently cross-tenant by email (the user is scoped to their own tenant
                // immediately after). Bypass the tenant filter for the credential lookup only.
                User? user;
                using (_tenantContext.BypassFilter())
                    user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

                // Customers are business-managed DATA records, never authentication identities.
                // Reject with the standard invalid-credentials message (don't leak that the email exists).
                if (user != null && user.Role == "Customer")
                {
                    ModelState.AddModelError("", "Invalid email or password");
                    return View(model);
                }

                if (user != null && _authService.VerifyPassword(model.Password, user.PasswordHash ?? string.Empty))
                {
                    if (!user.IsActive)
                    {
                        await _auditLogService.LogAsync("LoginBlocked", "User", user.Id,
                            $"Login blocked for deactivated account {user.Email}");
                        ModelState.AddModelError("", "Your account is disabled. Contact your administrator.");
                        return View(model);
                    }

                    // Set session values
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("UserName", user.Name);
                    HttpContext.Session.SetString("UserRole", user.Role);

                    // Establish the tenant scope for the rest of this request and future requests.
                    if (user.Role == "PlatformAdmin")
                    {
                        _tenantContext.SetTenant(null, isPlatformAdmin: true);
                    }
                    else if (user.TenantId.HasValue)
                    {
                        HttpContext.Session.SetInt32("TenantId", user.TenantId.Value);
                        _tenantContext.SetTenant(user.TenantId.Value, isPlatformAdmin: false);
                    }

                    // Set branch info if applicable
                    if (user.Role == "BranchManager")
                    {
                        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.ManagerId == user.Id && b.IsActive);
                        if (branch != null)
                        {
                            HttpContext.Session.SetInt32("ManagedBranchId", branch.Id);
                        }
                    }
                    else if (Cafe.Helpers.AppRoles.IsStaffLevel(user.Role))
                    {
                        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsActive);
                        if (staff != null)
                        {
                            HttpContext.Session.SetInt32("StaffBranchId", staff.BranchId);
                        }
                    }

                    TempData["Success"] = $"Welcome back, {user.Name}!";
                    await _auditLogService.LogAsync("Login", "User", user.Id, $"User {user.Name} logged in");

                    if (user.Role == "PlatformAdmin")
                        return RedirectToAction("Index", "Platform");
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid email or password");
            }

            return View(model);
        }

        // Closed platform: there is no self-registration. Accounts are provisioned by the
        // tenant Administrator (Users) or the Platform Admin (tenants). Anything that still
        // points at /Auth/Register lands back on the login page.
        [HttpGet, HttpPost]
        public IActionResult Register()
        {
            return RedirectToAction(nameof(Login));
        }

        // POST: /Auth/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _auditLogService.LogAsync("Logout", "User", null, "User logged out");
            HttpContext.Session.Clear();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // GET: /Auth/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
