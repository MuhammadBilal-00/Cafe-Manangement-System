using Cafe.Data;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public record KitchenItemDto(string Name, int Quantity, string? Notes, bool SentToKitchen);

    public record KitchenTicketDto(
        int OrderId, int BranchId, string OrderNumber, string? TableName, string ServiceType,
        string KitchenStatus, DateTime CreatedAt, int AgeMinutes, List<KitchenItemDto> Items);

    public interface IKitchenService
    {
        /// <summary>Active kitchen tickets for a branch (not yet Served, not Completed/Cancelled).</summary>
        Task<List<KitchenTicketDto>> GetTicketsAsync(int branchId);

        Task<KitchenTicketDto?> GetTicketAsync(int orderId);

        /// <summary>Forward-only KDS transition: New → Cooking → Ready → Served. Returns the updated
        /// ticket (for a SignalR push) or null on an invalid transition / missing order.</summary>
        Task<KitchenTicketDto?> UpdateStatusAsync(int orderId, string newStatus);

        /// <summary>Per-item routing: mark a line as sent to the kitchen.</summary>
        Task<bool> MarkItemSentAsync(int orderItemId);
    }

    public class KitchenService : IKitchenService
    {
        private readonly ApplicationDbContext _db;
        public KitchenService(ApplicationDbContext db) => _db = db;

        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["New"] = new[] { "Cooking", "Ready", "Served" },
            ["Cooking"] = new[] { "Ready", "Served" },
            ["Ready"] = new[] { "Served" },
            ["Served"] = Array.Empty<string>()
        };

        public async Task<List<KitchenTicketDto>> GetTicketsAsync(int branchId)
        {
            var orders = await _db.Orders
                .Where(o => o.BranchId == branchId
                    && o.KitchenStatus != "Served"
                    && o.Status != "Completed" && o.Status != "Cancelled"
                    && o.HoldState == "Active")
                .Include(o => o.Table)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            return orders.Select(ToDto).ToList();
        }

        public async Task<KitchenTicketDto?> GetTicketAsync(int orderId)
        {
            var o = await _db.Orders
                .Include(x => x.Table)
                .Include(x => x.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(x => x.Id == orderId);
            return o == null ? null : ToDto(o);
        }

        public async Task<KitchenTicketDto?> UpdateStatusAsync(int orderId, string newStatus)
        {
            var order = await _db.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return null;
            if (!Allowed.TryGetValue(order.KitchenStatus, out var next) || !next.Contains(newStatus))
                return null;

            order.KitchenStatus = newStatus;
            await _db.SaveChangesAsync();
            return ToDto(order);
        }

        public async Task<bool> MarkItemSentAsync(int orderItemId)
        {
            var rows = await _db.OrderItems
                .Where(oi => oi.Id == orderItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(oi => oi.SentToKitchen, true));
            return rows > 0;
        }

        private static KitchenTicketDto ToDto(Cafe.Models.Order o) => new(
            o.Id, o.BranchId, o.OrderNumber, o.Table?.Name, o.ServiceType, o.KitchenStatus, o.OrderDate,
            (int)Math.Max(0, (DateTime.Now - o.OrderDate).TotalMinutes),
            o.OrderItems.Select(oi => new KitchenItemDto(
                oi.MenuItem?.Name ?? "Item", oi.Quantity, oi.Notes, oi.SentToKitchen)).ToList());
    }
}
