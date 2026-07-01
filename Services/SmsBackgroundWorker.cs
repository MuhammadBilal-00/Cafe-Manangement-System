using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cafe.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cafe.Services
{
    /// <summary>
    /// Phase 10: background sender that drains the SmsQueue every 60 seconds through the active
    /// ISmsProvider (a real gateway swaps in without touching callers). Mirrors EmailBackgroundWorker:
    /// retries up to 3 times, marks sent/failed. Runs platform-wide, so it bypasses the tenant filter.
    /// </summary>
    public class SmsBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmsBackgroundWorker> _logger;
        private const int MaxRetries = 3;
        private const int IntervalSeconds = 60;
        private const int BatchSize = 50;

        public SmsBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<SmsBackgroundWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SmsBackgroundWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SmsBackgroundWorker main loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("SmsBackgroundWorker stopped.");
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sms = scope.ServiceProvider.GetRequiredService<ISmsProvider>();

            // Platform-wide drain: ignore the per-tenant query filter (background scope has no tenant).
            var pending = await db.SmsQueues.IgnoreQueryFilters()
                .Where(s => !s.IsSent && s.RetryCount < MaxRetries)
                .OrderBy(s => s.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (!pending.Any()) return;

            _logger.LogInformation("Processing {Count} pending SMS message(s) via {Provider}.", pending.Count, sms.Name);

            foreach (var msg in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var success = await sms.SendAsync(msg.ToPhone, msg.Body);
                    if (success)
                    {
                        msg.IsSent = true;
                        msg.SentAt = DateTime.UtcNow;
                        msg.ErrorMessage = null;
                    }
                    else
                    {
                        msg.RetryCount++;
                        msg.ErrorMessage = "SMS gateway not configured or send returned false.";
                    }
                }
                catch (Exception ex)
                {
                    msg.RetryCount++;
                    msg.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    _logger.LogWarning(ex, "SMS send failed for {Phone} (attempt {Retry}/{Max})",
                        msg.ToPhone, msg.RetryCount, MaxRetries);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
