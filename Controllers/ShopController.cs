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
    /// <summary>
    /// Phase 6: customer-facing portal — browse the menu, order online (feeds the same Order/KDS
    /// pipeline as Delivery), view history and track live status.
    /// </summary>
    [RequireCustomer]
    public class ShopController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IInvoiceService _invoices;
        private readonly ILoyaltyService _loyalty;
        private readonly INotificationService _notifications;
        private readonly ILogger<ShopController> _logger;

        public ShopController(ApplicationDbContext db, IInventoryService inventory, IInvoiceService invoices,
            ILoyaltyService loyalty, INotificationService notifications, ILogger<ShopController> logger)
        {
            _db = db;
            _inventory = inventory;
            _invoices = invoices;
            _loyalty = loyalty;
            _notifications = notifications;
            _logger = logger;
        }

        private int CustomerId => HttpContext.Session.GetUserId() ?? 0;

        public async Task<IActionResult> Index(int? branchId)
        {
            ViewBag.Branches = await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
            var effective = branchId ?? (await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync());
            ViewBag.SelectedBranchId = effective;
            ViewBag.Points = await _loyalty.BalanceAsync(CustomerId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMenu(int branchId)
        {
            var now = DateTime.Now;
            var rows = await _db.MenuItems.Include(m => m.Category)
                .Where(m => m.BranchId == branchId && m.Availability).ToListAsync();
            var items = rows.Where(m => MenuAvailability.IsAvailable(m, now))
                .Select(m => new { id = m.Id, name = m.Name, price = m.Price, category = m.Category.Name, desc = m.Description, img = m.ImageUrl });
            return Json(items);
        }

        private record Line(int menuItemId, int quantity);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(int branchId, string? serviceType, string? notes, string items)
        {
            if (!await _db.Branches.AnyAsync(b => b.Id == branchId && b.IsActive))
                return Json(new { success = false, message = "Branch unavailable." });
            List<Line> lines;
            try { lines = JsonSerializer.Deserialize<List<Line>>(items ?? "[]") ?? new(); } catch { return Json(new { success = false, message = "Invalid cart." }); }
            lines = lines.Where(l => l.menuItemId > 0 && l.quantity > 0).ToList();
            if (lines.Count == 0) return Json(new { success = false, message = "Your cart is empty." });

            var svc = serviceType is "Takeaway" or "Delivery" ? serviceType : "Delivery";
            var order = new Order
            {
                OrderNumber = await GenOrderNumberAsync(branchId),
                CustomerId = CustomerId, BranchId = branchId, OrderDate = DateTime.Now,
                Status = "Pending", ServiceType = svc, HoldState = "Active", KitchenStatus = "New",
                Notes = notes, TotalAmount = 0
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            decimal total = 0;
            foreach (var l in lines)
            {
                var mi = await _db.MenuItems.FirstOrDefaultAsync(m => m.Id == l.menuItemId && m.BranchId == branchId && m.Availability);
                if (mi == null) continue;
                var qty = Math.Max(1, l.quantity);
                _db.OrderItems.Add(new OrderItem { OrderId = order.Id, MenuItemId = mi.Id, Quantity = qty, Price = mi.Price });
                total += mi.Price * qty;
            }
            order.TotalAmount = total;
            await _db.SaveChangesAsync();

            await _inventory.DeductInventoryForOrder(order.Id, branchId, "Online");
            try { await _invoices.CreateForOrderAsync(order.Id, null, null, "Online", "Pending", CustomerId); } catch (Exception ex) { _logger.LogWarning(ex, "Online invoice failed"); }

            await _notifications.CreateNotificationAsync("New Online Order",
                $"Order #{order.OrderNumber} placed online ({svc}).", "Info", NotificationCategory.Order,
                branchId: branchId, createdBy: CustomerId, redirectUrl: "/Kitchen/Index", icon: "fas fa-globe");

            return Json(new { success = true, orderNumber = order.OrderNumber, orderId = order.Id });
        }

        public async Task<IActionResult> History()
        {
            var orders = await _db.Orders.Include(o => o.Branch).Include(o => o.OrderItems)
                .Where(o => o.CustomerId == CustomerId)
                .OrderByDescending(o => o.OrderDate).Take(50).ToListAsync();
            ViewBag.Points = await _loyalty.BalanceAsync(CustomerId);
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> TrackStatus(int id)
        {
            var o = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == CustomerId);
            if (o == null) return NotFound();
            return Json(new { status = o.Status, kitchenStatus = o.KitchenStatus, serviceType = o.ServiceType, orderNumber = o.OrderNumber });
        }

        private async Task<string> GenOrderNumberAsync(int branchId)
        {
            var branch = await _db.Branches.FindAsync(branchId);
            var code = new string((branch?.Name ?? "ONL").Where(char.IsLetter).Take(3).ToArray()).ToUpper();
            var prefix = $"{code}{DateTime.Now:yyyyMMdd}";
            var n = await _db.Orders.CountAsync(o => o.OrderNumber.StartsWith(prefix));
            for (var s = n + 1; ; s++) { var c = $"{prefix}{s:D3}"; if (!await _db.Orders.AnyAsync(o => o.OrderNumber == c)) return c; }
        }
    }
}
