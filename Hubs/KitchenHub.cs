using Microsoft.AspNetCore.SignalR;

namespace Cafe.Hubs
{
    /// <summary>
    /// Real-time Kitchen Display feed. Each connected screen joins its branch group so new
    /// tickets and status changes push instantly (no polling / Refresh button).
    /// </summary>
    public class KitchenHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            if (http != null)
            {
                var role = http.Session.GetString("UserRole");
                var managed = http.Session.GetInt32("ManagedBranchId");
                var staffBranch = http.Session.GetInt32("StaffBranchId");
                var branchId = managed ?? staffBranch;

                // Owner/PlatformAdmin (impersonating) can watch a specific branch via query string.
                if (branchId == null && int.TryParse(http.Request.Query["branchId"], out var qb))
                    branchId = qb;

                if (branchId.HasValue)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen_branch_{branchId.Value}");
            }
            await base.OnConnectedAsync();
        }

        /// <summary>Allow a client (e.g. an Owner switching branch) to (re)join a branch feed.</summary>
        public Task JoinBranch(int branchId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen_branch_{branchId}");
    }
}
