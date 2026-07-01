using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 7: employee documents (contracts, IDs, certificates) with expiry.</summary>
    [RequireManagerOrOwner]
    public class EmployeeDocumentController : BaseController
    {
        public EmployeeDocumentController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            var accessibleStaff = await GetAccessibleStaff();
            var ids = accessibleStaff.Select(s => s.Id).ToList();
            var docs = await _context.EmployeeDocuments.Include(d => d.Staff).ThenInclude(s => s!.User)
                .Where(d => ids.Contains(d.StaffId)).OrderByDescending(d => d.UploadedAt).Take(200).ToListAsync();
            ViewBag.Staff = accessibleStaff;
            return View(docs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int staffId, string title, string? docType, string? fileUrl, DateTime? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(title)) return Json(new { success = false, message = "Title is required." });
            var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff == null || !CanAccessBranch(staff.BranchId)) return Json(new { success = false, message = "Staff not accessible." });
            if (id == 0) _context.EmployeeDocuments.Add(new EmployeeDocument { StaffId = staffId, Title = title.Trim(), DocType = docType, FileUrl = fileUrl, ExpiresAt = expiresAt });
            else
            {
                var d = await _context.EmployeeDocuments.FirstOrDefaultAsync(x => x.Id == id);
                if (d == null) return Json(new { success = false });
                d.Title = title.Trim(); d.DocType = docType; d.FileUrl = fileUrl; d.ExpiresAt = expiresAt;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var d = await _context.EmployeeDocuments.FindAsync(id);
            if (d == null) return Json(new { success = false });
            _context.EmployeeDocuments.Remove(d); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
