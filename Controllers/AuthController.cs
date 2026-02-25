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

        public AuthController(ApplicationDbContext context, IAuthService authService, IAuditLogService auditLogService)
        {
            _context = context;
            _authService = authService;
            _auditLogService = auditLogService;
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
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user != null && _authService.VerifyPassword(model.Password, user.PasswordHash ?? string.Empty))
                {
                    // Set session values
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("UserName", user.Name);
                    HttpContext.Session.SetString("UserRole", user.Role);

                    // Set branch info if applicable
                    if (user.Role == "BranchManager")
                    {
                        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.ManagerId == user.Id && b.IsActive);
                        if (branch != null)
                        {
                            HttpContext.Session.SetInt32("ManagedBranchId", branch.Id);
                        }
                    }
                    else if (user.Role == "Staff")
                    {
                        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsActive);
                        if (staff != null)
                        {
                            HttpContext.Session.SetInt32("StaffBranchId", staff.BranchId);
                        }
                    }

                    TempData["Success"] = $"Welcome back, {user.Name}!";
                    await _auditLogService.LogAsync("Login", "User", user.Id, $"User {user.Name} logged in");
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid email or password");
            }

            return View(model);
        }

        // GET: /Auth/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Auth/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    return View(model);
                }

                var user = new User
                {
                    Name = model.Name,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = model.Role,
                    PasswordHash = _authService.HashPassword(model.Password),
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // If registering as customer, create customer record
                if (model.Role == "Customer")
                {
                    var customer = new Customer
                    {
                        UserId = user.Id,
                        Address = model.Address,
                        JoinDate = DateTime.Now,
                        LoyaltyPoints = 0,
                        IsActive = true
                    };
                    _context.Customers.Add(customer);
                }

                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync("Create", "User", user.Id, $"New user registered: {user.Name} ({user.Role})");

                TempData["Success"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }

            return View(model);
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
