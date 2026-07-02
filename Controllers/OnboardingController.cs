using Cafe.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>
    /// Closed platform: there is NO public self-serve signup — new businesses are provisioned
    /// only by the Platform Admin (sales-led onboarding) via the platform console. The old
    /// public signup wizard is retired; any lingering /Onboarding link lands on the console's
    /// provisioning form (or the login gate for anonymous visitors).
    /// </summary>
    [RequirePlatformAdmin]
    public class OnboardingController : Controller
    {
        [HttpGet, HttpPost]
        public IActionResult Index()
        {
            return RedirectToAction("CreateTenant", "Platform");
        }
    }
}
