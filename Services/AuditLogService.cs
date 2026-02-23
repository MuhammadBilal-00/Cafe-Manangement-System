using System;
using System.Threading.Tasks;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Http;

namespace Cafe.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int MaxDetailsLength = 500;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityType, int? entityId, string? details)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var userIdStr = session?.GetString("UserId");

            var log = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details?.Length > MaxDetailsLength ? details.Substring(0, MaxDetailsLength) : details,
                UserId = int.TryParse(userIdStr, out var uid) ? uid : null,
                UserName = session?.GetString("UserName"),
                UserRole = session?.GetString("UserRole"),
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
