using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 8: documents, memos, reminders and the knowledge base in one workspace.</summary>
    [RequireStaffOrAbove]
    public class EssentialsController : BaseController
    {
        private readonly INotificationService _notifications;
        public EssentialsController(ApplicationDbContext context, INotificationService notifications) : base(context)
        {
            _notifications = notifications;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Documents = await _context.Documents.OrderByDescending(d => d.CreatedAt).Take(100).ToListAsync();
            ViewBag.Memos = await _context.Memos.OrderByDescending(m => m.Pinned).ThenByDescending(m => m.CreatedAt).Take(100).ToListAsync();
            ViewBag.Reminders = await _context.Reminders.OrderBy(r => r.Done).ThenBy(r => r.DueAt).Take(100).ToListAsync();
            ViewBag.Articles = await _context.KnowledgeBaseArticles.OrderBy(a => a.Category).ThenBy(a => a.Title).Take(100).ToListAsync();
            return View();
        }

        // ── Documents ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDocument(int id, string title, string? category, string? fileUrl, string? notes)
        {
            if (string.IsNullOrWhiteSpace(title)) return Json(new { success = false, message = "Title is required." });
            if (id == 0) _context.Documents.Add(new Document { Title = title.Trim(), Category = category, FileUrl = fileUrl, Notes = notes, CreatedById = GetCurrentUserId() });
            else { var d = await _context.Documents.FirstOrDefaultAsync(x => x.Id == id); if (d == null) return Json(new { success = false }); d.Title = title.Trim(); d.Category = category; d.FileUrl = fileUrl; d.Notes = notes; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id) => await Remove(_context.Documents, id);

        // ── Memos ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMemo(int id, string title, string body, bool pinned)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) return Json(new { success = false, message = "Title and body are required." });
            if (id == 0) _context.Memos.Add(new Memo { Title = title.Trim(), Body = body, Pinned = pinned, CreatedById = GetCurrentUserId() });
            else { var m = await _context.Memos.FirstOrDefaultAsync(x => x.Id == id); if (m == null) return Json(new { success = false }); m.Title = title.Trim(); m.Body = body; m.Pinned = pinned; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMemo(int id) => await Remove(_context.Memos, id);

        // ── Reminders ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveReminder(int id, string title, DateTime dueAt)
        {
            if (string.IsNullOrWhiteSpace(title)) return Json(new { success = false, message = "Title is required." });
            if (id == 0)
            {
                _context.Reminders.Add(new Reminder { Title = title.Trim(), DueAt = dueAt, OwnerId = GetCurrentUserId() });
                await _context.SaveChangesAsync();
                await _notifications.CreateNotificationAsync("Reminder set", $"{title} — due {dueAt:dd MMM HH:mm}", "Info",
                    NotificationCategory.System, userId: GetCurrentUserId(), createdBy: GetCurrentUserId(), redirectUrl: "/Essentials/Index", icon: "fas fa-bell");
            }
            else { var r = await _context.Reminders.FirstOrDefaultAsync(x => x.Id == id); if (r == null) return Json(new { success = false }); r.Title = title.Trim(); r.DueAt = dueAt; await _context.SaveChangesAsync(); }
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReminder(int id)
        {
            var r = await _context.Reminders.FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) return Json(new { success = false });
            r.Done = !r.Done;
            await _context.SaveChangesAsync();
            return Json(new { success = true, done = r.Done });
        }

        // ── Knowledge base ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveArticle(int id, string title, string? category, string body, bool isPublished)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) return Json(new { success = false, message = "Title and body are required." });
            if (id == 0) _context.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle { Title = title.Trim(), Category = category, Body = body, IsPublished = isPublished, CreatedById = GetCurrentUserId() });
            else { var a = await _context.KnowledgeBaseArticles.FirstOrDefaultAsync(x => x.Id == id); if (a == null) return Json(new { success = false }); a.Title = title.Trim(); a.Category = category; a.Body = body; a.IsPublished = isPublished; a.UpdatedAt = DateTime.Now; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArticle(int id) => await Remove(_context.KnowledgeBaseArticles, id);

        private async Task<IActionResult> Remove<T>(DbSet<T> set, int id) where T : class
        {
            var e = await set.FindAsync(id);
            if (e == null) return Json(new { success = false });
            set.Remove(e);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
