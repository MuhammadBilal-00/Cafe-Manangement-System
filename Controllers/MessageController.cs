using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 8: internal direct messaging. New messages ping the recipient in real time
    /// via the existing SignalR notification pipeline.</summary>
    [RequireStaffOrAbove]
    public class MessageController : BaseController
    {
        private readonly INotificationService _notifications;
        public MessageController(ApplicationDbContext context, INotificationService notifications) : base(context)
        {
            _notifications = notifications;
        }

        private int Me => GetCurrentUserId() ?? 0;

        public async Task<IActionResult> Index()
        {
            // People to message: staff/managers/owners in this tenant (excluding self).
            ViewBag.People = await _context.Users
                .Where(u => u.Id != Me && u.Role != "Customer" && u.Role != "PlatformAdmin" && u.IsActive)
                .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name, u.Role }).ToListAsync();
            ViewBag.Unread = await _context.Messages.CountAsync(m => m.ToUserId == Me && !m.IsRead);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Thread(int userId)
        {
            var msgs = await _context.Messages
                .Where(m => (m.FromUserId == Me && m.ToUserId == userId) || (m.FromUserId == userId && m.ToUserId == Me))
                .OrderBy(m => m.CreatedAt).Take(200).ToListAsync();
            // Mark the incoming ones read.
            foreach (var m in msgs.Where(m => m.ToUserId == Me && !m.IsRead)) m.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(msgs.Select(m => new { m.Id, mine = m.FromUserId == Me, m.Body, at = m.CreatedAt.ToString("dd MMM HH:mm") }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int toUserId, string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Json(new { success = false, message = "Message is empty." });
            var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == toUserId);
            if (recipient == null) return Json(new { success = false, message = "Recipient not found." });

            _context.Messages.Add(new Message { FromUserId = Me, ToUserId = toUserId, Body = body.Trim(), CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            var fromName = HttpContext.Session.GetUserName() ?? "Someone";
            await _notifications.CreateNotificationAsync($"Message from {fromName}",
                body.Length > 80 ? body[..80] + "…" : body, "Info", NotificationCategory.System,
                userId: toUserId, createdBy: Me, redirectUrl: "/Message/Index", icon: "fas fa-comment");

            return Json(new { success = true });
        }
    }
}
