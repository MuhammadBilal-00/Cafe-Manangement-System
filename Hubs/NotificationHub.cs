using Microsoft.AspNetCore.SignalR;

namespace Cafe.Hubs
{
    /// <summary>
    /// SignalR hub for real-time notification delivery.
    /// Clients connect and join groups based on UserId, Role, and BranchId.
    /// </summary>
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                var userId = httpContext.Session.GetInt32("UserId");
                var userRole = httpContext.Session.GetString("UserRole");
                var managedBranchId = httpContext.Session.GetInt32("ManagedBranchId");
                var staffBranchId = httpContext.Session.GetInt32("StaffBranchId");

                // Join user-specific group
                if (userId.HasValue)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");

                // Join role group
                if (!string.IsNullOrEmpty(userRole))
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{userRole}");

                // Join branch group
                var branchId = managedBranchId ?? staffBranchId;
                if (branchId.HasValue)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"branch_{branchId.Value}");

                // Owner joins a special "all" group
                if (userRole == "Owner")
                    await Groups.AddToGroupAsync(Context.ConnectionId, "role_All");
            }

            await base.OnConnectedAsync();
        }
    }
}
