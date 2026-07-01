using Cafe.Data;
using Cafe.Models;
using Microsoft.Extensions.Configuration;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 6: SMS/WhatsApp delivery behind an adapter (Twilio/local gateway). Ships a stub that
    /// queues + logs; wiring a real gateway later doesn't change callers. Messages are queued like email.
    /// </summary>
    public interface ISmsProvider
    {
        string Name { get; }
        Task<bool> SendAsync(string toPhone, string body);
    }

    /// <summary>Default stub: succeeds locally (logs) and no-ops when no gateway is configured.</summary>
    public class LoggingSmsProvider : ISmsProvider
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LoggingSmsProvider> _logger;
        public LoggingSmsProvider(IConfiguration config, ILogger<LoggingSmsProvider> logger) { _config = config; _logger = logger; }

        public string Name => "Logging";
        public Task<bool> SendAsync(string toPhone, string body)
        {
            // A real provider would POST to the gateway using _config["Sms:*"].
            _logger.LogInformation("SMS → {Phone}: {Body}", toPhone, body);
            return Task.FromResult(true);
        }
    }

    /// <summary>Queues SMS messages (a background sender / campaign delivery drains the queue).</summary>
    public interface ISmsQueueService
    {
        Task QueueAsync(string toPhone, string body);
    }

    public class SmsQueueService : ISmsQueueService
    {
        private readonly ApplicationDbContext _db;
        public SmsQueueService(ApplicationDbContext db) => _db = db;
        public async Task QueueAsync(string toPhone, string body)
        {
            _db.SmsQueues.Add(new SmsQueue { ToPhone = toPhone, Body = body, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
        }
    }
}
