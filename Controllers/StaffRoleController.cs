using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[RequireManagerOrOwner]
public class StaffRoleController : Controller
{
    private readonly ApplicationDbContext _context;

    public StaffRoleController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _context.StaffRoles
            .Include(r => r.CreatedByUser)
            .Include(r => r.StaffMembers)
            .Where(r => r.IsActive)
            .OrderBy(r => r.RoleName)
            .ToListAsync();

        return View(roles);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffRole staffRole)
    {
        if (ModelState.IsValid)
        {
            staffRole.CreatedBy = GetCurrentUserId();
            staffRole.CreatedDate = DateTime.Now;
            staffRole.IsActive = true;

            _context.Add(staffRole);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Role created successfully!";
            return RedirectToAction(nameof(Index));
        }
        return View(staffRole);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var role = await _context.StaffRoles.FindAsync(id);
        if (role == null)
        {
            return NotFound();
        }
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StaffRole staffRole)
    {
        if (id != staffRole.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(staffRole);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Role updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RoleExists(staffRole.Id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(staffRole);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _context.StaffRoles.FindAsync(id);
        if (role != null)
        {
            if (role.IsSystemRole)
            {
                TempData["Error"] = "System roles cannot be deleted.";
            }
            else if (await _context.Staff.AnyAsync(s => s.StaffRoleId == id && s.IsActive))
            {
                TempData["Error"] = "Cannot delete role assigned to active staff.";
            }
            else
            {
                role.IsActive = false;
                _context.Update(role);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Role deleted successfully!";
            }
        }
        return RedirectToAction(nameof(Index));
    }

    private bool RoleExists(int id)
    {
        return _context.StaffRoles.Any(e => e.Id == id);
    }

    private int? GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }
}