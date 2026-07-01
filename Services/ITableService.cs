using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public interface ITableService
    {
        Task<List<RestaurantTable>> GetByBranchAsync(int branchId, bool activeOnly = true);
        Task<RestaurantTable?> GetAsync(int id);
        Task<RestaurantTable> CreateAsync(RestaurantTable table);
        Task<bool> UpdateAsync(RestaurantTable table);
        Task<bool> SetStatusAsync(int tableId, string status);
        Task<bool> DeactivateAsync(int tableId);
        static readonly string[] Statuses = { "Available", "Occupied", "Reserved", "Dirty" };
    }

    public class TableService : ITableService
    {
        private readonly ApplicationDbContext _db;
        public TableService(ApplicationDbContext db) => _db = db;

        public Task<List<RestaurantTable>> GetByBranchAsync(int branchId, bool activeOnly = true) =>
            _db.RestaurantTables
                .Where(t => t.BranchId == branchId && (!activeOnly || t.IsActive))
                .OrderBy(t => t.Zone).ThenBy(t => t.Name)
                .ToListAsync();

        public Task<RestaurantTable?> GetAsync(int id) =>
            _db.RestaurantTables.FirstOrDefaultAsync(t => t.Id == id);

        public async Task<RestaurantTable> CreateAsync(RestaurantTable table)
        {
            _db.RestaurantTables.Add(table);
            await _db.SaveChangesAsync();
            return table;
        }

        public async Task<bool> UpdateAsync(RestaurantTable table)
        {
            var existing = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.Id == table.Id);
            if (existing == null) return false;
            existing.Name = table.Name;
            existing.Capacity = table.Capacity;
            existing.Zone = table.Zone;
            existing.IsActive = table.IsActive;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetStatusAsync(int tableId, string status)
        {
            if (!ITableService.Statuses.Contains(status)) return false;
            // Conditional update: never blindly overwrite — only flip an existing table's status.
            var rows = await _db.RestaurantTables
                .Where(t => t.Id == tableId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, status));
            return rows > 0;
        }

        public async Task<bool> DeactivateAsync(int tableId)
        {
            var rows = await _db.RestaurantTables
                .Where(t => t.Id == tableId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
            return rows > 0;
        }
    }
}
