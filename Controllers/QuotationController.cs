using System.Text.Json;
using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 4: quotations that convert into orders (a credit sale on account).</summary>
    [RequireManagerOrOwner]
    public class QuotationController : BaseController
    {
        private readonly IInventoryService _inventory;
        private readonly IInvoiceService _invoices;
        private readonly IAuditLogService _audit;

        public QuotationController(ApplicationDbContext context, IInventoryService inventory, IInvoiceService invoices, IAuditLogService audit) : base(context)
        {
            _inventory = inventory;
            _invoices = invoices;
            _audit = audit;
        }

        private record Line(int menuItemId, int quantity, decimal price, decimal lineDiscount);

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            var quotes = await _context.Quotations.Include(q => q.Branch).Include(q => q.Customer).Include(q => q.Items)
                .Where(q => branchIds.Contains(q.BranchId)).OrderByDescending(q => q.CreatedAt).Take(100).ToListAsync();
            ViewBag.Branches = await GetAccessibleBranches();
            return View(quotes);
        }

        [HttpGet]
        public async Task<IActionResult> GetMenu(int branchId)
        {
            if (!CanAccessBranch(branchId)) return Forbid();
            var items = await _context.MenuItems.Where(m => m.BranchId == branchId && m.Availability)
                .OrderBy(m => m.Name).Select(m => new { id = m.Id, name = m.Name, price = m.Price }).ToListAsync();
            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int branchId, int? customerId, string? notes, DateTime? validUntil, string items)
        {
            if (!CanAccessBranch(branchId)) return Json(new { success = false, message = "Access denied." });
            List<Line> lines;
            try { lines = JsonSerializer.Deserialize<List<Line>>(items ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid items." }); }
            lines = lines.Where(l => l.menuItemId > 0 && l.quantity > 0).ToList();
            if (lines.Count == 0) return Json(new { success = false, message = "Add at least one item." });

            var subtotal = lines.Sum(l => (l.price * l.quantity) - l.lineDiscount);
            var quote = new Quotation
            {
                BranchId = branchId, CustomerId = customerId, QuotationNumber = await GenNumberAsync(branchId),
                Status = "Draft", Subtotal = Math.Round(subtotal, 2), Notes = notes,
                ValidUntil = validUntil ?? DateTime.Today.AddDays(14), CreatedById = GetCurrentUserId(), CreatedAt = DateTime.Now
            };
            _context.Quotations.Add(quote);
            await _context.SaveChangesAsync();
            foreach (var l in lines)
                _context.QuotationItems.Add(new QuotationItem { QuotationId = quote.Id, MenuItemId = l.menuItemId, Quantity = l.quantity, Price = l.price, LineDiscount = l.lineDiscount });
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Create", "Quotation", quote.Id, $"Quote {quote.QuotationNumber}", branchId);
            return Json(new { success = true, id = quote.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(int id)
        {
            var quote = await _context.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null || !CanAccessBranch(quote.BranchId)) return Json(new { success = false, message = "Not found." });
            if (quote.Status == "Converted") return Json(new { success = false, message = "Already converted." });

            var order = new Order
            {
                OrderNumber = await GenOrderNumberAsync(quote.BranchId),
                CustomerId = quote.CustomerId, BranchId = quote.BranchId, OrderDate = DateTime.Now,
                Status = "Pending", ServiceType = "Takeaway", HoldState = "Active", KitchenStatus = "New",
                TotalAmount = quote.Subtotal
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            foreach (var qi in quote.Items)
                _context.OrderItems.Add(new OrderItem { OrderId = order.Id, MenuItemId = qi.MenuItemId, Quantity = qi.Quantity, Price = qi.Price, LineDiscount = qi.LineDiscount });
            await _context.SaveChangesAsync();

            await _inventory.DeductInventoryForOrder(order.Id, quote.BranchId, HttpContext.Session.GetUserName() ?? "System");
            try { await _invoices.CreateForOrderAsync(order.Id, null, null, "Credit", "Pending", GetCurrentUserId()); } catch { }

            quote.Status = "Converted"; quote.ConvertedOrderId = order.Id;
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Convert", "Quotation", quote.Id, $"Converted to order {order.OrderNumber}", quote.BranchId);
            return Json(new { success = true, orderNumber = order.OrderNumber });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var quote = await _context.Quotations.FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null || !CanAccessBranch(quote.BranchId)) return Json(new { success = false });
            if (quote.Status == "Converted") return Json(new { success = false, message = "Converted quotes can't be cancelled." });
            quote.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private async Task<string> GenNumberAsync(int branchId)
        {
            var prefix = $"QUO-{DateTime.Now:yyyyMMdd}";
            var n = await _context.Quotations.CountAsync(q => q.QuotationNumber.StartsWith(prefix));
            for (var s = n + 1; ; s++) { var c = $"{prefix}-{s:D3}"; if (!await _context.Quotations.AnyAsync(q => q.QuotationNumber == c)) return c; }
        }
        private async Task<string> GenOrderNumberAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            var code = new string((branch?.Name ?? "ORD").Where(char.IsLetter).Take(3).ToArray()).ToUpper();
            var prefix = $"{code}{DateTime.Now:yyyyMMdd}";
            var n = await _context.Orders.CountAsync(o => o.OrderNumber.StartsWith(prefix));
            for (var s = n + 1; ; s++) { var c = $"{prefix}{s:D3}"; if (!await _context.Orders.AnyAsync(o => o.OrderNumber == c)) return c; }
        }
    }
}
