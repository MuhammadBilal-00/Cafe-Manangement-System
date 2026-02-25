using System.Threading.Tasks;

namespace Cafe.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string action, string entityType, int? entityId, string? details, int? branchId = null);
    }
}
