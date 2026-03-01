using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Hubs;
using Cafe.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cafe.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<Notification> CreateNotificationAsync(
            string title,
            string message,
            string type = "Info",
            NotificationCategory category = NotificationCategory.System,
            int? userId = null,
            string? roleTarget = null,
            int? branchId = null,
            int? createdBy = null,
            string? redirectUrl = null,
            string? icon = null)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                UserId = userId,
                RoleTarget = roleTarget,
                BranchId = branchId,
                CreatedBy = createdBy,
                RedirectUrl = redirectUrl,
                Icon = icon ?? GetDefaultIcon(type),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Push real-time update via SignalR
            await PushNotificationAsync(notification);

            // Queue email if applicable
            await QueueEmailIfNeededAsync(notification, category);

            return notification;
        }

        public async Task<(List<NotificationDto> Items, int TotalCount)> GetNotificationsAsync(
            int currentUserId, string userRole, int? userBranchId,
            int page = 1, int pageSize = 20,
            bool? isRead = null, string? type = null,
            DateTime? from = null, DateTime? to = null)
        {
            var query = BuildVisibilityQuery(currentUserId, userRole, userBranchId);

            if (isRead.HasValue)
                query = query.Where(n => n.IsRead == isRead.Value);
            if (!string.IsNullOrEmpty(type))
                query = query.Where(n => n.Type == type);
            if (from.HasValue)
                query = query.Where(n => n.CreatedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(n => n.CreatedAt <= to.Value.Date.AddDays(1));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => ToDto(n))
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> GetUnreadCountAsync(int currentUserId, string userRole, int? userBranchId)
        {
            return await BuildVisibilityQuery(currentUserId, userRole, userBranchId)
                .Where(n => !n.IsRead)
                .CountAsync();
        }

        public async Task MarkAsReadAsync(int notificationId, int currentUserId, string userRole, int? userBranchId)
        {
            var notification = await BuildVisibilityQuery(currentUserId, userRole, userBranchId)
                .FirstOrDefaultAsync(n => n.Id == notificationId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int currentUserId, string userRole, int? userBranchId)
        {
            var unread = await BuildVisibilityQuery(currentUserId, userRole, userBranchId)
                .Where(n => !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            if (unread.Any())
                await _context.SaveChangesAsync();
        }

        public async Task<List<NotificationDto>> GetRecentAsync(int currentUserId, string userRole, int? userBranchId, int count = 5)
        {
            return await BuildVisibilityQuery(currentUserId, userRole, userBranchId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .Select(n => ToDto(n))
                .ToListAsync();
        }

        // ─── Private Helpers ───────────────────────────────────────

        /// <summary>
        /// Build query that enforces role-based visibility:
        /// Owner → sees everything
        /// BranchManager → sees notifications targeted to them, their role, their branch, or broadcast
        /// Staff → sees only their own user-targeted notifications or broadcast
        /// </summary>
        private IQueryable<Notification> BuildVisibilityQuery(int currentUserId, string userRole, int? userBranchId)
        {
            var query = _context.Notifications.AsQueryable();

            if (userRole == "Owner")
            {
                // Owner sees everything
                return query;
            }

            if (userRole == "BranchManager")
            {
                return query.Where(n =>
                    n.UserId == currentUserId ||                                     // targeted to this user
                    (n.UserId == null && n.RoleTarget == "BranchManager") ||          // targeted to all managers
                    (n.UserId == null && n.RoleTarget == null && n.BranchId == userBranchId) || // branch broadcast
                    (n.UserId == null && n.RoleTarget == null && n.BranchId == null)  // global broadcast
                );
            }

            // Staff and others
            return query.Where(n =>
                n.UserId == currentUserId ||                                          // targeted to this user
                (n.UserId == null && n.RoleTarget == "Staff" && (n.BranchId == null || n.BranchId == userBranchId)) || // staff role in their branch
                (n.UserId == null && n.RoleTarget == null && n.BranchId == userBranchId) || // branch broadcast
                (n.UserId == null && n.RoleTarget == null && n.BranchId == null)       // global broadcast
            );
        }

        /// <summary>Push notification to the correct SignalR groups.</summary>
        private async Task PushNotificationAsync(Notification notification)
        {
            try
            {
                var dto = new
                {
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.IsRead,
                    notification.CreatedAt,
                    notification.RedirectUrl,
                    notification.Icon,
                    TimeAgo = FormatTimeAgo(notification.CreatedAt)
                };

                // User-specific
                if (notification.UserId.HasValue)
                {
                    await _hubContext.Clients.Group($"user_{notification.UserId.Value}")
                        .SendAsync("ReceiveNotification", dto);
                    return;
                }

                // Role-specific
                if (!string.IsNullOrEmpty(notification.RoleTarget))
                {
                    await _hubContext.Clients.Group($"role_{notification.RoleTarget}")
                        .SendAsync("ReceiveNotification", dto);
                    // Also send to Owner who sees everything
                    await _hubContext.Clients.Group("role_All")
                        .SendAsync("ReceiveNotification", dto);
                    return;
                }

                // Branch-specific
                if (notification.BranchId.HasValue)
                {
                    await _hubContext.Clients.Group($"branch_{notification.BranchId.Value}")
                        .SendAsync("ReceiveNotification", dto);
                    await _hubContext.Clients.Group("role_All")
                        .SendAsync("ReceiveNotification", dto);
                    return;
                }

                // Global broadcast
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push SignalR notification {Id}", notification.Id);
            }
        }

        /// <summary>Queue email for users who have email notifications enabled.</summary>
        private async Task QueueEmailIfNeededAsync(Notification notification, NotificationCategory category)
        {
            try
            {
                // If targeted to specific user, check their preference
                if (notification.UserId.HasValue)
                {
                    var pref = await _context.NotificationPreferences
                        .FirstOrDefaultAsync(p => p.UserId == notification.UserId.Value);

                    if (pref != null && pref.EmailEnabled && IsCategoryEnabled(pref, category))
                    {
                        var user = await _context.Users.FindAsync(notification.UserId.Value);
                        if (user != null && !string.IsNullOrEmpty(user.Email))
                        {
                            _context.EmailQueues.Add(new EmailQueue
                            {
                                ToEmail = user.Email,
                                ToName = user.Name,
                                Subject = notification.Title,
                                Body = BuildEmailBody(notification, user.Name),
                                NotificationId = notification.Id
                            });
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                // For role/branch/broadcast notifications, queue for all matching users with email enabled
                else
                {
                    var usersQuery = _context.Users.AsQueryable();

                    if (!string.IsNullOrEmpty(notification.RoleTarget))
                        usersQuery = usersQuery.Where(u => u.Role == notification.RoleTarget);

                    if (notification.BranchId.HasValue)
                    {
                        var branchStaffIds = await _context.Staff
                            .Where(s => s.BranchId == notification.BranchId.Value && s.IsActive)
                            .Select(s => s.UserId)
                            .ToListAsync();
                        usersQuery = usersQuery.Where(u => branchStaffIds.Contains(u.Id));
                    }

                    var users = await usersQuery.ToListAsync();

                    foreach (var user in users)
                    {
                        var pref = await _context.NotificationPreferences
                            .FirstOrDefaultAsync(p => p.UserId == user.Id);

                        // Default: email enabled if no preference record exists
                        bool shouldEmail = pref == null || (pref.EmailEnabled && IsCategoryEnabled(pref, category));

                        if (shouldEmail && !string.IsNullOrEmpty(user.Email))
                        {
                            _context.EmailQueues.Add(new EmailQueue
                            {
                                ToEmail = user.Email,
                                ToName = user.Name,
                                Subject = notification.Title,
                                Body = BuildEmailBody(notification, user.Name),
                                NotificationId = notification.Id
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue emails for notification {Id}", notification.Id);
            }
        }

        private static bool IsCategoryEnabled(NotificationPreference pref, NotificationCategory category)
        {
            return category switch
            {
                NotificationCategory.Order => pref.OrderNotifications,
                NotificationCategory.Staff => pref.StaffNotifications,
                NotificationCategory.Inventory => pref.InventoryNotifications,
                NotificationCategory.Financial => pref.FinancialNotifications,
                NotificationCategory.System => pref.SystemNotifications,
                _ => true
            };
        }

        private static string BuildEmailBody(Notification notification, string userName)
        {
            var typeColor = notification.Type switch
            {
                "Success" => "#16a34a",
                "Warning" => "#d97706",
                "Error" => "#dc2626",
                _ => "#6366f1"
            };

            var redirectLink = !string.IsNullOrEmpty(notification.RedirectUrl)
                ? $"<a href=\"{notification.RedirectUrl}\" style=\"display:inline-block;margin-top:16px;padding:10px 24px;background:{typeColor};color:#fff;text-decoration:none;border-radius:8px;font-weight:600;\">View Details</a>"
                : "";

            return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Arial,sans-serif;background:#f5f3f0;"">
<div style=""max-width:520px;margin:24px auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.06);"">
    <div style=""padding:24px 32px;background:{typeColor};color:#fff;"">
        <h1 style=""margin:0;font-size:18px;font-weight:700;"">☕ Cafe Manager</h1>
    </div>
    <div style=""padding:28px 32px;"">
        <p style=""margin:0 0 8px;color:#6b7280;font-size:14px;"">Hi {userName},</p>
        <h2 style=""margin:0 0 12px;font-size:17px;color:#1e293b;"">{notification.Title}</h2>
        <p style=""margin:0;color:#475569;font-size:14px;line-height:1.6;"">{notification.Message}</p>
        {redirectLink}
    </div>
    <div style=""padding:16px 32px;background:#faf9f7;border-top:1px solid #e5e7eb;text-align:center;"">
        <p style=""margin:0;font-size:12px;color:#9ca3af;"">Cafe Management System — Do not reply to this email</p>
    </div>
</div>
</body>
</html>";
        }

        private static string GetDefaultIcon(string type)
        {
            return type switch
            {
                "Success" => "fas fa-check-circle",
                "Warning" => "fas fa-exclamation-triangle",
                "Error" => "fas fa-times-circle",
                _ => "fas fa-info-circle"
            };
        }

        internal static NotificationDto ToDto(Notification n)
        {
            return new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                RedirectUrl = n.RedirectUrl,
                Icon = n.Icon,
                TimeAgo = FormatTimeAgo(n.CreatedAt)
            };
        }

        internal static string FormatTimeAgo(DateTime utcTime)
        {
            var diff = DateTime.UtcNow - utcTime;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return utcTime.ToString("MMM dd, yyyy");
        }
    }
}
