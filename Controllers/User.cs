using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public UserController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // User management (list/create/edit/delete any account) is Owner-only.
        // Profile/Settings/ChangePassword below stay open to any logged-in user.
        [RequireOwner]
        public async Task<IActionResult> Index(string role)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                users = users.Where(u => u.Role == role);
            }

            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
            ViewBag.SelectedRole = role;

            return View(await users.OrderBy(u => u.Name).ToListAsync());
        }

        [RequireOwner]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .Include(u => u.ManagedBranches)
                .Include(u => u.StaffRecords)
                .ThenInclude(s => s.Branch)
                .Include(u => u.Orders)
                .Include(u => u.Feedbacks)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        [RequireOwner]
        public IActionResult Create()
        {
            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Create([Bind("Name,Email,Phone,Role,PasswordHash")] User user)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
                    return View(user);
                }

                user.PasswordHash = _authService.HashPassword(user.PasswordHash ?? "changeme");

                _context.Add(user);
                await _context.SaveChangesAsync();

                // If user is a customer, create customer record
                if (user.Role == "Customer")
                {
                    var customer = new Customer
                    {
                        UserId = user.Id,
                        LoyaltyPoints = 0
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
            return View(user);
        }

        [RequireOwner]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,Phone,Role,PasswordHash,CreatedDate")] User user)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists for other users
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == user.Email && u.Id != user.Id);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "Email already exists");
                        ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
                        return View(user);
                    }

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff", "Customer" };
            return View(user);
        }

        [RequireOwner]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        public async Task<IActionResult> Profile()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                var userIdInt = HttpContext.Session.GetInt32("UserId");
                if (!userIdInt.HasValue)
                    return RedirectToAction("Login", "Auth");
                userId = userIdInt.Value;
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            return View(user);
        }

        public async Task<IActionResult> Settings()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                var userIdInt = HttpContext.Session.GetInt32("UserId");
                if (!userIdInt.HasValue)
                    return RedirectToAction("Login", "Auth");
                userId = userIdInt.Value;
            }

            var user = await _context.Users.FindAsync(userId);
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
