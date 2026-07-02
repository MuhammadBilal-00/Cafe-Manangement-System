using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Cafe.Helpers;

namespace Cafe.Attributes
{
    /// <summary>
    /// Phase 6 customer portal gate. The portal is DISABLED in this closed, staff-only
    /// version: customers are business-managed data records and never authenticate, so
    /// every portal route bounces to the staff login. CRM, loyalty and receivables are
    /// all staff-side and unaffected. Flip <see cref="CustomerPortalEnabled"/> to restore
    /// the portal (customer logins would also need to be re-enabled in AuthController).
    /// </summary>
    public class RequireCustomerAttribute : ActionFilterAttribute
    {
        public static readonly bool CustomerPortalEnabled = false;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!CustomerPortalEnabled)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

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
