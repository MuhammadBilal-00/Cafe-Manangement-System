using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 9 (61): delivery &amp; rider management, extending the order pipeline.</summary>
    [RequireManagerOrOwner]
    public class DeliveryController : BaseController
    {
        public DeliveryController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index()
        {
            var branchIds = (await GetAccessibleBranches()).Select(b => b.Id).ToList();
            ViewBag.Riders = await _context.Riders.Where(r => r.BranchId == null || branchIds.Contains(r.BranchId.Value)).OrderBy(r => r.Name).ToListAsync();
            // Delivery orders needing/undergoing dispatch.
            var deliveries = await _context.Deliveries.Include(d => d.Order).Include(d => d.Rider)
                .Where(d => branchIds.Contains(d.Order!.BranchId)).OrderByDescending(d => d.CreatedAt).Take(100).ToListAsync();
            ViewBag.Deliveries = deliveries;
            // Delivery orders without a delivery record yet.
            ViewBag.Pending = await _context.Orders.Include(o => o.Customer)
                .Where(o => branchIds.Contains(o.BranchId) && o.ServiceType == "Delivery" && o.Status != "Completed" && o.Status != "Cancelled"
                    && !_context.Deliveries.Any(d => d.OrderId == o.Id))
                .OrderByDescending(o => o.OrderDate).Take(50).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRider(int id, string name, string? phone, string? vehicle)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (id == 0) _context.Riders.Add(new Rider { Name = name.Trim(), Phone = phone, Vehicle = vehicle });
            else { var r = await _context.Riders.FirstOrDefaultAsync(x => x.Id == id); if (r == null) return Json(new { success = false }); r.Name = name.Trim(); r.Phone = phone; r.Vehicle = vehicle; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int orderId, int riderId, decimal fee, string? address)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || !CanAccessBranch(order.BranchId)) return Json(new { success = false, message = "Order not accessible." });
            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.OrderId == orderId);
            if (delivery == null) { delivery = new Delivery { OrderId = orderId }; _context.Deliveries.Add(delivery); }
            delivery.RiderId = riderId; delivery.Fee = fee; delivery.Address = address; delivery.Status = "Assigned"; delivery.AssignedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (status is not ("Assigned" or "PickedUp" or "Delivered" or "Failed")) return Json(new { success = false });
            var d = await _context.Deliveries.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == id);
            if (d == null || d.Order == null || !CanAccessBranch(d.Order.BranchId)) return Json(new { success = false });
            d.Status = status;
            if (status == "Delivered") { d.DeliveredAt = DateTime.Now; d.Order.Status = "Completed"; }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
