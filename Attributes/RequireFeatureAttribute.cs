using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Cafe.Helpers;
using Cafe.Services;

namespace Cafe.Attributes
{
    /// <summary>
    /// Server-side feature guard: blocks the action unless the current tenant's plan unlocks the
    /// feature. The sidebar hides locked modules, but this is the real enforcement so a locked
    /// module can't be reached by URL. Platform admins (not impersonating) always pass.
    /// </summary>
    public class RequireFeatureAttribute : ActionFilterAttribute
    {
        private readonly string _featureKey;

        public RequireFeatureAttribute(string featureKey) => _featureKey = featureKey;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var gate = context.HttpContext.RequestServices.GetService(typeof(IFeatureGate)) as IFeatureGate;
            if (gate != null && !await gate.HasFeatureAsync(_featureKey))
            {
                var session = context.HttpContext.Session;
                if (!session.IsAuthenticated())
                {
                    context.Result = new RedirectToActionResult("Login", "Auth", null);
                    return;
                }

                context.HttpContext.Items["UpgradeFeature"] = _featureKey;
                context.Result = new RedirectToActionResult("Upgrade", "Subscription", new { feature = _featureKey });
                return;
            }

            await next();
        }
    }
}
