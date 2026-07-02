// Controllers/BaseController.cs
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        public BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        protected int? GetCurrentUserId()
        {
            return HttpContext.Session.GetUserId();
        }

        protected string? GetCurrentUserRole()
        {
            return HttpContext.Session.GetUserRole();
        }

        protected bool CanAccessBranch(int branchId)
        {
            var userRole = GetCurrentUserRole();

            if (userRole == "Owner")
                return true;

            if (userRole == "BranchManager")
            {
                var managedBranchId = HttpContext.Session.GetManagedBranchId();
                return managedBranchId.HasValue && managedBranchId.Value == branchId;
            }

            if (AppRoles.IsStaffLevel(userRole))
            {
                var staffBranchId = HttpContext.Session.GetStaffBranchId();
                return staffBranchId.HasValue && staffBranchId.Value == branchId;
            }

            return false;
        }

        protected IActionResult AccessDenied()
        {
            TempData["Error"] = "You don't have permission to access this resource.";
            return RedirectToAction("AccessDenied", "Auth");
        }

        protected void SetSuccessMessage(string message)
        {
            TempData["Success"] = message;
        }

        protected void SetErrorMessage(string message)
        {
            TempData["Error"] = message;
        }

        /// <summary>
        /// Returns branches the current user is allowed to access, always queried fresh from DB.
        /// Owner → all active branches. Manager → their branch. Staff → their branch.
        /// </summary>
        protected async Task<List<Branch>> GetAccessibleBranches()
        {
            var role = GetCurrentUserRole();

            if (role == "Owner")
                return await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    return await _context.Branches.Where(b => b.Id == branchId.Value && b.IsActive).ToListAsync();
            }

            if (AppRoles.IsStaffLevel(role))
            {
                var branchId = HttpContext.Session.GetStaffBranchId();
                if (branchId.HasValue)
                    return await _context.Branches.Where(b => b.Id == branchId.Value && b.IsActive).ToListAsync();
            }

            return new List<Branch>();
        }

        /// <summary>
        /// Returns branches as SelectListItem for dropdown use, always fresh from DB.
        /// </summary>
        protected async Task<List<SelectListItem>> GetBranchSelectList()
        {
            var branches = await GetAccessibleBranches();
            return branches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToList();
        }

        /// <summary>
        /// Returns staff the current user is allowed to see, always queried fresh from DB.
        /// Owner → all active staff. Manager → staff in their branch. Staff → themselves only.
        /// </summary>
        protected async Task<List<Staff>> GetAccessibleStaff()
        {
            var role = GetCurrentUserRole();
            var query = _context.Staff.Include(s => s.User).Where(s => s.IsActive);

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    query = query.Where(s => s.BranchId == branchId.Value);
            }
            else if (AppRoles.IsStaffLevel(role))
            {
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                    query = query.Where(s => s.UserId == userId.Value);
            }

            return await query.OrderBy(s => s.User.Name).ToListAsync();
        }

        /// <summary>
        /// Returns active suppliers visible to the current user.
        /// Pass includeInactiveSupplierId to also include a specific inactive supplier that is
        /// already linked to the entity being edited — prevents silently nulling the FK on save.
        /// </summary>
        protected async Task<List<Supplier>> GetAccessibleSuppliers(int? includeInactiveSupplierId = null)
        {
            var role = GetCurrentUserRole();
            var query = _context.Suppliers
                .Include(s => s.Branch)
                .Where(s => s.IsActive || (includeInactiveSupplierId.HasValue && s.Id == includeInactiveSupplierId.Value));

            if (role == "BranchManager")
            {
                var branchId = HttpContext.Session.GetManagedBranchId();
                if (branchId.HasValue)
                    query = query.Where(s =>
                        s.BranchId == branchId.Value ||
                        (includeInactiveSupplierId.HasValue && s.Id == includeInactiveSupplierId.Value));
            }

            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        /// <summary>
        /// Returns the effective branch ID for the current user.
        /// BranchManager → their managed branch (request param ignored).
        /// Owner/other → the requested branch ID (null = all branches).
        /// </summary>
        protected int? GetEffectiveBranchId(int? requestedBranchId)
        {
            if (GetCurrentUserRole() == "BranchManager")
                return HttpContext.Session.GetManagedBranchId();
            return requestedBranchId;
        }
    }
}