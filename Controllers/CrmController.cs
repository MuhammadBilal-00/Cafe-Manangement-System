using Cafe.Attributes;
using Cafe.Data;
using Cafe.Helpers;
using Cafe.Models;
using Cafe.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Controllers
{
    /// <summary>Phase 6 CRM: leads, follow-ups, campaigns.</summary>
    [RequireFeature("Marketing")]
    [RequireManagerOrOwner]
    public class CrmController : BaseController
    {
        private readonly ISmsQueueService _sms;
        private readonly IAuditLogService _audit;

        public CrmController(ApplicationDbContext context, ISmsQueueService sms, IAuditLogService audit) : base(context)
        {
            _sms = sms;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Leads = await _context.Leads.OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync();
            ViewBag.Campaigns = await _context.Campaigns.OrderByDescending(c => c.CreatedAt).Take(50).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveLead(int id, string name, string? email, string? phone, string? source, string status, string? notes)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Name is required." });
            if (status is not ("New" or "Contacted" or "Qualified" or "Won" or "Lost")) status = "New";
            if (id == 0) _context.Leads.Add(new Lead { Name = name.Trim(), Email = email, Phone = phone, Source = source, Status = status, Notes = notes });
            else
            {
                var l = await _context.Leads.FirstOrDefaultAsync(x => x.Id == id);
                if (l == null) return Json(new { success = false, message = "Not found." });
                l.Name = name.Trim(); l.Email = email; l.Phone = phone; l.Source = source; l.Status = status; l.Notes = notes;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireOwner]
        public async Task<IActionResult> DeleteLead(int id)
        {
            var l = await _context.Leads.FindAsync(id);
            if (l == null) return Json(new { success = false });
            _context.Leads.Remove(l);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCampaign(int id, string name, string channel, string segment, string? subject, string body)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(body)) return Json(new { success = false, message = "Name and body are required." });
            if (channel is not ("Email" or "SMS")) channel = "Email";
            if (id == 0) _context.Campaigns.Add(new Campaign { Name = name.Trim(), Channel = channel, Segment = segment, Subject = subject, Body = body, Status = "Draft" });
            else
            {
                var c = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == id);
                if (c == null || c.Status == "Sent") return Json(new { success = false, message = "Sent campaigns can't be edited." });
                c.Name = name.Trim(); c.Channel = channel; c.Segment = segment; c.Subject = subject; c.Body = body;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        /// <summary>Send a campaign: queue email/SMS to the segment (drained by the background sender).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendCampaign(int id)
        {
            var c = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return Json(new { success = false, message = "Not found." });
            if (c.Status == "Sent") return Json(new { success = false, message = "Already sent." });

            int count = 0;
            if (c.Segment == "AllCustomers")
            {
                var recipients = await _context.Customers.Include(x => x.User).Where(x => x.IsActive)
                    .Select(x => new { x.User.Email, x.User.Phone, x.User.Name }).ToListAsync();
                foreach (var r in recipients)
                {
                    if (c.Channel == "SMS" && !string.IsNullOrWhiteSpace(r.Phone) && r.Phone != "N/A")
                    { await _sms.QueueAsync(r.Phone, Render(c.Body, r.Name)); count++; }
                    else if (c.Channel == "Email" && !string.IsNullOrWhiteSpace(r.Email))
                    { _context.EmailQueues.Add(new EmailQueue { ToEmail = r.Email, ToName = r.Name, Subject = c.Subject ?? c.Name, Body = Render(c.Body, r.Name) }); count++; }
                }
            }
            else // Leads
            {
                var leads = await _context.Leads.Where(l => l.Status != "Lost").ToListAsync();
                foreach (var l in leads)
                {
                    if (c.Channel == "SMS" && !string.IsNullOrWhiteSpace(l.Phone)) { await _sms.QueueAsync(l.Phone!, Render(c.Body, l.Name)); count++; }
                    else if (c.Channel == "Email" && !string.IsNullOrWhiteSpace(l.Email)) { _context.EmailQueues.Add(new EmailQueue { ToEmail = l.Email!, ToName = l.Name, Subject = c.Subject ?? c.Name, Body = Render(c.Body, l.Name) }); count++; }
                }
            }

            c.Status = "Sent"; c.Recipients = count; c.SentAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Send", "Campaign", c.Id, $"Queued {count} {c.Channel} messages");
            return Json(new { success = true, message = $"Queued {count} {c.Channel} message(s)." });
        }

        private static string Render(string body, string name) => body.Replace("{CustomerName}", name).Replace("{Name}", name);
    }
}
