using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 9 (62): shipments linked to orders or stock transfers.</summary>
    [RequireManagerOrOwner]
    public class ShipmentController : BaseController
    {
        public ShipmentController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index() =>
            View(await _context.Shipments.OrderByDescending(s => s.CreatedAt).Take(100).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int? orderId, int? stockTransferId, string? carrier, string? trackingNumber, string status)
        {
            if (status is not ("Preparing" or "Shipped" or "InTransit" or "Delivered" or "Returned")) status = "Preparing";
            if (id == 0)
            {
                _context.Shipments.Add(new Shipment { OrderId = orderId, StockTransferId = stockTransferId, Carrier = carrier, TrackingNumber = trackingNumber, Status = status, ShippedAt = status == "Shipped" ? DateTime.Now : null });
            }
            else
            {
                var s = await _context.Shipments.FirstOrDefaultAsync(x => x.Id == id);
                if (s == null) return Json(new { success = false });
                s.OrderId = orderId; s.StockTransferId = stockTransferId; s.Carrier = carrier; s.TrackingNumber = trackingNumber;
                if (s.Status != status)
                {
                    s.Status = status;
                    if (status == "Shipped" && s.ShippedAt == null) s.ShippedAt = DateTime.Now;
                    if (status == "Delivered") s.DeliveredAt = DateTime.Now;
                }
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _context.Shipments.FindAsync(id);
            if (s == null) return Json(new { success = false });
            _context.Shipments.Remove(s); await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
