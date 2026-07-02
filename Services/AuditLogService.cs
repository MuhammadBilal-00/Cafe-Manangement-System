using System;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cafe.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int MaxDetailsLength = 500;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string action, string entityType, int? entityId, string? details, int? branchId = null)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userId = session?.GetInt32("UserId");

                // Auto-resolve branchId from session if not provided
                if (!branchId.HasValue)
                {
                    var role = session?.GetString("UserRole");
                    if (role == "BranchManager")
                        branchId = session?.GetManagedBranchId();
                    else if (Cafe.Helpers.AppRoles.IsStaffLevel(role))
                        branchId = session?.GetStaffBranchId();
                }

                var log = new AuditLog
                {
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details?.Length > MaxDetailsLength ? details.Substring(0, MaxDetailsLength) : details,
                    UserId = userId,
                    UserName = session?.GetString("UserName"),
                    UserRole = session?.GetString("UserRole"),
                    BranchId = branchId,
                    IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit logging must never crash the main operation
                _logger.LogError(ex, "Failed to write audit log: {Action} {EntityType} {EntityId}", action, entityType, entityId);
            }
        }
    }
}
