using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Models;
using Cafe.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Cafe.Services;

namespace Cafe.Controllers
{
    public class StaffController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;

        public StaffController(ApplicationDbContext context, IAuthService authService) : base(context)
        {
            _context = context;
            _authService = authService;
        }

        // GET: Staff
        public async Task<IActionResult> Index()
        {
            var staff = await _context.Staff
                .Include(s => s.User)
                .Include(s => s.StaffRole)
                .Include(s => s.Branch)
                //.Where(s => s.IsActive)
                .ToListAsync();

            return View(staff);
        }

        // GET: Staff/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

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

        // GET: Staff/Create
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        // POST: Staff/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    PopulateDropdowns();
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
                    EmployeeId = model.EmployeeId,
                    HireDate = DateTime.Now,
                    IsActive = model.IsActive
                };

                _context.Staff.Add(staff);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Staff member created successfully!";
                return RedirectToAction(nameof(Index));
            }

            PopulateDropdowns();
            return View(model);
        }

        // GET: Staff/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var staff = await _context.Staff
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (staff == null)
            {
                return NotFound();
            }

            var model = new StaffEditViewModel
            {
                Id = staff.Id,
                StaffRoleId = staff.StaffRoleId,
                BranchId = staff.BranchId,
                EmploymentType = staff.EmploymentType,
                Department = staff.Department,
                EmployeeId = staff.EmployeeId,
                PerformanceRating = staff.PerformanceRating,
                IsActive = staff.IsActive,
                Name = staff.User.Name,
                Email = staff.User.Email,
                Phone = staff.User.Phone
            };

            PopulateDropdowns();
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

            PopulateDropdowns();
            return View(model);
        }

        // GET: Staff/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _context.Staff.FindAsync(id);
            if (staff != null)
            {
                // Soft delete (set IsActive to false)
                staff.IsActive = false;
                staff.TerminationDate = DateTime.Now;

                _context.Staff.Update(staff);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Staff member deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool StaffExists(int id)
        {
            return _context.Staff.Any(e => e.Id == id);
        }

        private void PopulateDropdowns()
        {
            ViewBag.StaffRoles = _context.StaffRoles
                .Where(r => r.IsActive)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.RoleName
                })
                .ToList();

            ViewBag.Branches = _context.Branches
                .Where(b => b.IsActive)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToList();
        }
    }
}