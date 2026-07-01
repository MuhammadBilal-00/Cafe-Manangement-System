using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Cafe.Helpers;

namespace Cafe.Attributes
{
    /// <summary>Restricts an action/controller to the SaaS platform operator (PlatformAdmin role).</summary>
    public class RequirePlatformAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.Session.IsPlatformAdmin())
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
