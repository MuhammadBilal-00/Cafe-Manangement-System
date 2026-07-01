using Cafe.Attributes;
using Cafe.Data;
using Cafe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 6: editable, tokenized notification templates.</summary>
    [RequireFeature("Marketing")]
    [RequireManagerOrOwner]
    public class NotificationTemplateController : BaseController
    {
        public NotificationTemplateController(ApplicationDbContext context) : base(context) { }

        public async Task<IActionResult> Index() =>
            View(await _context.NotificationTemplates.OrderBy(t => t.Name).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string key, string name, string channel, string? subject, string body, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(body))
                return Json(new { success = false, message = "Key, name and body are required." });
            if (channel is not ("Email" or "SMS" or "InApp")) channel = "Email";
            key = key.Trim();
            if (await _context.NotificationTemplates.AnyAsync(t => t.Key == key && t.Id != id))
                return Json(new { success = false, message = "That key already exists." });

            if (id == 0) _context.NotificationTemplates.Add(new NotificationTemplate { Key = key, Name = name.Trim(), Channel = channel, Subject = subject, Body = body, IsActive = isActive });
            else
            {
                var t = await _context.NotificationTemplates.FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return Json(new { success = false, message = "Not found." });
                t.Key = key; t.Name = name.Trim(); t.Channel = channel; t.Subject = subject; t.Body = body; t.IsActive = isActive;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> Delete(int id)
        {
            var t = await _context.NotificationTemplates.FindAsync(id);
            if (t == null) return Json(new { success = false });
            _context.NotificationTemplates.Remove(t);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
