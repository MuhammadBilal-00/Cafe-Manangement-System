using Cafe.Attributes;
using Cafe.Data;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>Phase 10: owner-only page to populate the tenant with demo data for every module.</summary>
    [RequireOwner]
    public class DemoController : BaseController
    {
        private readonly IDemoDataService _demo;

        public DemoController(ApplicationDbContext context, IDemoDataService demo) : base(context)
        {
            _demo = demo;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Seed()
        {
            var log = await _demo.SeedAsync(GetCurrentUserId());
            return Json(new { success = true, log });
        }
    }
}
