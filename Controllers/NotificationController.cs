using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    [RequireStaffOrAbove]
    public class NotificationController : BaseController
    {
        private readonly INotificationService _notificationService;
        private readonly IAuditLogService _auditLogService;

        public NotificationController(
            ApplicationDbContext context,
            INotificationService notificationService,
            IAuditLogService auditLogService) : base(context)
        {
            _notificationService = notificationService;
            _auditLogService = auditLogService;
        }

        // ─── Full notifications page ──────────────────────────
        public async Task<IActionResult> Index(int page = 1, bool? isRead = null, string? type = null,
            DateTime? from = null, DateTime? to = null)
        {
            var userId = GetCurrentUserId() ?? 0;
            var role = GetCurrentUserRole() ?? "Staff";
            var branchId = GetUserBranchId();

            var (items, total) = await _notificationService.GetNotificationsAsync(
                userId, role, branchId, page, 20, isRead, type, from, to);

            var unreadCount = await _notificationService.GetUnreadCountAsync(userId, role, branchId);

            ViewBag.Notifications = items;
            ViewBag.TotalCount = total;
            ViewBag.Page = page;
            ViewBag.PageSize = 20;
            ViewBag.TotalPages = (int)Math.Ceiling(total / 20.0);
            ViewBag.UnreadCount = unreadCount;
            ViewBag.FilterIsRead = isRead;
            ViewBag.FilterType = type;
            ViewBag.FilterFrom = from;
            ViewBag.FilterTo = to;

            return View();
        }

        // ─── API: Get recent (for bell dropdown) ─────────────
        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var userId = GetCurrentUserId() ?? 0;
            var role = GetCurrentUserRole() ?? "Staff";
            var branchId = GetUserBranchId();

            var recent = await _notificationService.GetRecentAsync(userId, role, branchId, 5);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId, role, branchId);

            return Json(new { notifications = recent, unreadCount });
        }

        // ─── API: Get unread count (for polling) ─────────────
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = GetCurrentUserId() ?? 0;
            var role = GetCurrentUserRole() ?? "Staff";
            var branchId = GetUserBranchId();

            var count = await _notificationService.GetUnreadCountAsync(userId, role, branchId);
            return Json(new { count });
        }

        // ─── API: Mark single as read ────────────────────────
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId() ?? 0;
            var role = GetCurrentUserRole() ?? "Staff";
            var branchId = GetUserBranchId();

            await _notificationService.MarkAsReadAsync(id, userId, role, branchId);
            return Json(new { success = true });
        }

        // ─── API: Mark all as read ───────────────────────────
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId() ?? 0;
            var role = GetCurrentUserRole() ?? "Staff";
            var branchId = GetUserBranchId();

            await _notificationService.MarkAllAsReadAsync(userId, role, branchId);
            return Json(new { success = true });
        }

        // ─── Admin: Send notification page (Owner only) ──────
        [RequireOwner]
        public async Task<IActionResult> Admin()
        {
            ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            ViewBag.Users = await _context.Users.OrderBy(u => u.Name).ToListAsync();
            ViewBag.Roles = new[] { "Owner", "BranchManager", "Staff" };

            // Email queue stats
            ViewBag.EmailTotal = await _context.EmailQueues.CountAsync();
            ViewBag.EmailSent = await _context.EmailQueues.CountAsync(e => e.IsSent);
            ViewBag.EmailPending = await _context.EmailQueues.CountAsync(e => !e.IsSent && e.RetryCount < 3);
            ViewBag.EmailFailed = await _context.EmailQueues.CountAsync(e => !e.IsSent && e.RetryCount >= 3);

            return View();
        }

        // ─── Admin: Send notification (Owner only) ───────────
        [HttpPost]
        [RequireOwner]
        public async Task<IActionResult> SendNotification(
            string title, string message, string type,
            string targetType, int? targetUserId, string? targetRole, int? targetBranchId,
            string deliveryMethod)
        {
            var createdBy = GetCurrentUserId();

            int? userId = targetType == "user" ? targetUserId : null;
            string? roleTarget = targetType == "role" ? targetRole : null;
            int? branchId = targetType == "branch" ? targetBranchId : null;
            // targetType == "all" → all nulls = global broadcast

            await _notificationService.CreateNotificationAsync(
                title, message, type,
                NotificationCategory.System,
                userId, roleTarget, branchId, createdBy,
                icon: "fas fa-bullhorn");

            await _auditLogService.LogAsync("Send", "Notification", null,
                $"Admin notification: \"{title}\" to {targetType}" +
                (userId.HasValue ? $" user #{userId}" : "") +
                (!string.IsNullOrEmpty(roleTarget) ? $" role {roleTarget}" : "") +
                (branchId.HasValue ? $" branch #{branchId}" : ""));

            SetSuccessMessage("Notification sent successfully!");
            return RedirectToAction(nameof(Admin));
        }

        // ─── Notification Preferences ────────────────────────
        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var pref = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (pref == null)
            {
                pref = new NotificationPreference { UserId = userId.Value };
                _context.NotificationPreferences.Add(pref);
                await _context.SaveChangesAsync();
            }

            return View(pref);
        }

        [HttpPost]
        public async Task<IActionResult> SavePreferences(NotificationPreference model)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var pref = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (pref == null)
            {
                pref = new NotificationPreference { UserId = userId.Value };
                _context.NotificationPreferences.Add(pref);
            }

            pref.InAppEnabled = model.InAppEnabled;
            pref.EmailEnabled = model.EmailEnabled;
            pref.OrderNotifications = model.OrderNotifications;
            pref.StaffNotifications = model.StaffNotifications;
            pref.InventoryNotifications = model.InventoryNotifications;
            pref.FinancialNotifications = model.FinancialNotifications;
            pref.SystemNotifications = model.SystemNotifications;

            await _context.SaveChangesAsync();

            SetSuccessMessage("Notification preferences saved!");
            return RedirectToAction(nameof(Preferences));
        }

        // ─── Helper ──────────────────────────────────────────
        private int? GetUserBranchId()
        {
            var role = GetCurrentUserRole();
            if (role == "BranchManager")
                return HttpContext.Session.GetManagedBranchId();
            if (role == "Staff")
                return HttpContext.Session.GetStaffBranchId();
            return null;
        }
    }
}
