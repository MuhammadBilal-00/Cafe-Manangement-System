using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Cafe.Helpers;

namespace Cafe.Attributes
{
    /// <summary>Phase 6: restricts the customer portal to signed-in customers.</summary>
    public class RequireCustomerAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            if (!session.IsAuthenticated())
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }
            if (!session.IsCustomer())
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
