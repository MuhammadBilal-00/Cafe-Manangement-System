using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>Phase 9 (56): sets the UI culture via a cookie (read by RequestLocalization).</summary>
    public class LanguageController : Controller
    {
        [HttpGet]
        public IActionResult Set(string culture, string? returnUrl)
        {
            var allowed = new[] { "en", "ur", "ar" };
            if (!allowed.Contains(culture)) culture = "en";
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl);
        }
    }
}
