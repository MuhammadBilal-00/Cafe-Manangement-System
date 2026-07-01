using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cafe.Controllers
{
    /// <summary>Phase 10: shared binary upload endpoint used by document / employee-record forms.</summary>
    [RequireStaffOrAbove]
    public class FileController : BaseController
    {
        private readonly IFileStorageService _storage;

        public FileController(ApplicationDbContext context, IFileStorageService storage) : base(context)
        {
            _storage = storage;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(16 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile? file, string? folder)
        {
            var (ok, url, error) = await _storage.SaveAsync(file, folder ?? "documents");
            return Json(ok ? new { success = true, url } : new { success = false, message = error });
        }
    }
}
