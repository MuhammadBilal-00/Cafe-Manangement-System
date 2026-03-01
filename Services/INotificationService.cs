using Cafe.Models;
using System.Threading.Tasks;

namespace Cafe.Services
{
    /// <summary>
    /// Notification categories for preference matching.
    /// </summary>
    public enum NotificationCategory
    {
        Order,
        Staff,
        Inventory,
        Financial,
        System
    }

    public interface INotificationService
    {
        /// <summary>
        /// Create and persist a notification, then push it via SignalR in real-time.
        /// </summary>
        Task<Notification> CreateNotificationAsync(
            string title,
            string message,
            string type = "Info",
            NotificationCategory category = NotificationCategory.System,
            int? userId = null,
            string? roleTarget = null,
            int? branchId = null,
            int? createdBy = null,
            string? redirectUrl = null,
            string? icon = null);

        /// <summary>Get notifications visible to the current user, with pagination.</summary>
        Task<(List<NotificationDto> Items, int TotalCount)> GetNotificationsAsync(
            int currentUserId, string userRole, int? userBranchId,
            int page = 1, int pageSize = 20,
            bool? isRead = null, string? type = null,
            DateTime? from = null, DateTime? to = null);

        /// <summary>Get unread count for the current user.</summary>
        Task<int> GetUnreadCountAsync(int currentUserId, string userRole, int? userBranchId);

        /// <summary>Mark a single notification as read.</summary>
        Task MarkAsReadAsync(int notificationId, int currentUserId, string userRole, int? userBranchId);

        /// <summary>Mark all visible notifications as read.</summary>
        Task MarkAllAsReadAsync(int currentUserId, string userRole, int? userBranchId);

        /// <summary>Get top N recent notifications for the bell dropdown.</summary>
        Task<List<NotificationDto>> GetRecentAsync(int currentUserId, string userRole, int? userBranchId, int count = 5);
    }

    /// <summary>DTO for returning notifications to the frontend.</summary>
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Info";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RedirectUrl { get; set; }
        public string? Icon { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
    }
}
