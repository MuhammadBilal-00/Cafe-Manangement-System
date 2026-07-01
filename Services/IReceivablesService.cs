using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cafe.Services
{
    public record DueRow(int Id, string Name, string? Contact, decimal Due);

    /// <summary>
    /// Phase 4 accounts receivable (customers) &amp; payable (suppliers).
    /// AR(customer) = Σ invoice totals − Σ payments − Σ approved sell-returns.
    /// AP(supplier) = Σ purchase costs − Σ supplier payments − Σ approved purchase-returns.
    /// Balances are cached briefly and the cache is invalidated when a payment is recorded.
    /// </summary>
    public interface IReceivablesService
    {
        Task<decimal> CustomerDueAsync(int customerUserId);
        Task<decimal> SupplierDueAsync(int supplierId);
        Task<List<DueRow>> CustomersWithDueAsync();
        Task<List<DueRow>> SuppliersWithDueAsync();
        Task<(bool ok, string message)> ReceiveCustomerPaymentAsync(int customerUserId, decimal amount, string method, string? reference);
        Task<(bool ok, string message)> RecordSupplierPaymentAsync(int supplierId, decimal amount, string method, string? reference, int? branchId, int? userId);
    }

    public class ReceivablesService : IReceivablesService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        public ReceivablesService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<decimal> CustomerDueAsync(int customerUserId)
        {
            if (_cache.TryGetValue($"cust-due-{customerUserId}", out decimal cached)) return cached;
            var inv = await _db.Invoices.Where(i => i.PaymentStatus != "Cancelled" && i.Order.CustomerId == customerUserId)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
            var paid = await _db.Payments.Where(p => p.Invoice.Order.CustomerId == customerUserId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
            var ret = await _db.SellReturns.Where(r => r.CustomerId == customerUserId && r.Status == "Approved")
                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
            var due = Math.Round(inv - paid - ret, 2);
            _cache.Set($"cust-due-{customerUserId}", due, TimeSpan.FromSeconds(30));
            return due;
        }

        public async Task<decimal> SupplierDueAsync(int supplierId)
        {
            if (_cache.TryGetValue($"supp-due-{supplierId}", out decimal cached)) return cached;
            var pur = await _db.Purchases.Where(p => p.SupplierId == supplierId && p.Status != "Cancelled")
                .SumAsync(p => (decimal?)p.TotalCost) ?? 0;
            var paid = await _db.SupplierPayments.Where(p => p.SupplierId == supplierId).SumAsync(p => (decimal?)p.Amount) ?? 0;
            var ret = await _db.PurchaseReturns.Where(r => r.SupplierId == supplierId && r.Status == "Approved")
                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
            var due = Math.Round(pur - paid - ret, 2);
            _cache.Set($"supp-due-{supplierId}", due, TimeSpan.FromSeconds(30));
            return due;
        }

        public async Task<List<DueRow>> CustomersWithDueAsync()
        {
            var inv = await _db.Invoices.Where(i => i.PaymentStatus != "Cancelled" && i.Order.CustomerId != null)
                .GroupBy(i => i.Order.CustomerId!.Value).Select(g => new { Id = g.Key, V = g.Sum(x => x.TotalAmount) }).ToListAsync();
            var pay = await _db.Payments.Where(p => p.Invoice.Order.CustomerId != null)
                .GroupBy(p => p.Invoice.Order.CustomerId!.Value).Select(g => new { Id = g.Key, V = g.Sum(x => x.Amount) }).ToListAsync();
            var ret = await _db.SellReturns.Where(r => r.Status == "Approved" && r.CustomerId != null)
                .GroupBy(r => r.CustomerId!.Value).Select(g => new { Id = g.Key, V = g.Sum(x => x.TotalAmount) }).ToListAsync();

            var due = Combine(inv.ToDictionary(x => x.Id, x => x.V), pay.ToDictionary(x => x.Id, x => x.V), ret.ToDictionary(x => x.Id, x => x.V));
            var ids = due.Keys.ToList();
            var users = await _db.Users.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Name, u.Phone }).ToListAsync();
            return users.Select(u => new DueRow(u.Id, u.Name, u.Phone, due[u.Id]))
                .Where(r => r.Due > 0.01m).OrderByDescending(r => r.Due).ToList();
        }

        public async Task<List<DueRow>> SuppliersWithDueAsync()
        {
            var pur = await _db.Purchases.Where(p => p.SupplierId != null && p.Status != "Cancelled")
                .GroupBy(p => p.SupplierId!.Value).Select(g => new { Id = g.Key, V = g.Sum(x => x.TotalCost) }).ToListAsync();
            var pay = await _db.SupplierPayments.GroupBy(p => p.SupplierId).Select(g => new { Id = g.Key, V = g.Sum(x => x.Amount) }).ToListAsync();
            var ret = await _db.PurchaseReturns.Where(r => r.Status == "Approved" && r.SupplierId != null)
                .GroupBy(r => r.SupplierId!.Value).Select(g => new { Id = g.Key, V = g.Sum(x => x.TotalAmount) }).ToListAsync();

            var due = Combine(pur.ToDictionary(x => x.Id, x => x.V), pay.ToDictionary(x => x.Id, x => x.V), ret.ToDictionary(x => x.Id, x => x.V));
            var ids = due.Keys.ToList();
            var sups = await _db.Suppliers.Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.Phone }).ToListAsync();
            return sups.Select(s => new DueRow(s.Id, s.Name, s.Phone, due[s.Id]))
                .Where(r => r.Due > 0.01m).OrderByDescending(r => r.Due).ToList();
        }

        public async Task<(bool, string)> ReceiveCustomerPaymentAsync(int customerUserId, decimal amount, string method, string? reference)
        {
            if (amount <= 0) return (false, "Amount must be positive.");
            // Allocate to the customer's oldest not-fully-paid invoices.
            var invoices = await _db.Invoices.Include(i => i.Payments)
                .Where(i => i.Order.CustomerId == customerUserId && i.PaymentStatus != "Cancelled")
                .OrderBy(i => i.CreatedAt).ToListAsync();

            decimal remaining = Math.Round(amount, 2);
            foreach (var inv in invoices)
            {
                if (remaining <= 0) break;
                var paidSoFar = inv.Payments.Sum(p => p.Amount);
                var due = inv.TotalAmount - paidSoFar;
                if (due <= 0) continue;
                var apply = Math.Min(remaining, due);
                _db.Payments.Add(new Payment { InvoiceId = inv.Id, Method = method, Amount = apply, Reference = reference, PaidAt = DateTime.Now });
                remaining -= apply;
                if (paidSoFar + apply + 0.01m >= inv.TotalAmount) { inv.PaymentStatus = "Paid"; if (inv.PaidAt == null) inv.PaidAt = DateTime.Now; }
            }
            // Any overpayment lands on the newest invoice as customer credit.
            if (remaining > 0 && invoices.Count > 0)
                _db.Payments.Add(new Payment { InvoiceId = invoices.Last().Id, Method = method, Amount = remaining, Reference = reference, PaidAt = DateTime.Now });

            await _db.SaveChangesAsync();
            _cache.Remove($"cust-due-{customerUserId}");
            return (true, $"Received Rs. {amount:N0} from customer.");
        }

        public async Task<(bool, string)> RecordSupplierPaymentAsync(int supplierId, decimal amount, string method, string? reference, int? branchId, int? userId)
        {
            if (amount <= 0) return (false, "Amount must be positive.");
            _db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierId = supplierId, Amount = Math.Round(amount, 2), Method = method,
                Reference = reference, BranchId = branchId, CreatedById = userId, PaidAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            _cache.Remove($"supp-due-{supplierId}");
            return (true, $"Paid Rs. {amount:N0} to supplier.");
        }

        private static Dictionary<int, decimal> Combine(Dictionary<int, decimal> plus, Dictionary<int, decimal> minus1, Dictionary<int, decimal> minus2)
        {
            var result = new Dictionary<int, decimal>();
            foreach (var kv in plus) result[kv.Key] = kv.Value;
            foreach (var kv in minus1) result[kv.Key] = (result.TryGetValue(kv.Key, out var v) ? v : 0) - kv.Value;
            foreach (var kv in minus2) result[kv.Key] = (result.TryGetValue(kv.Key, out var v) ? v : 0) - kv.Value;
            return result;
        }
    }
}
