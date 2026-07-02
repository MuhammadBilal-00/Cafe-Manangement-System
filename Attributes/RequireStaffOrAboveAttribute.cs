// Attributes/RequireStaffOrAboveAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Cafe.Helpers;

namespace Cafe.Attributes
{
    public class RequireStaffOrAboveAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userRole = session.GetUserRole();

            // Staff-level covers all internal operational roles (Staff, HR, InventoryManager, Cashier).
            if (userRole != AppRoles.Owner && userRole != AppRoles.BranchManager && !AppRoles.IsStaffLevel(userRole))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}