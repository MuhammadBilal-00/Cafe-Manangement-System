// Controllers/BaseController.cs
using Cafe.Data;
using Cafe.Helpers;
using Microsoft.AspNetCore.Mvc;
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

            if (userRole == "Staff")
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
    }
}