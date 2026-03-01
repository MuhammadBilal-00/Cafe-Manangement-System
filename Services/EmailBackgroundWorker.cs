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
    /// Background worker that processes the EmailQueue every 60 seconds.
    /// Retries failed emails up to 3 times before giving up.
    /// </summary>
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailBackgroundWorker> _logger;
        private const int MaxRetries = 3;
        private const int IntervalSeconds = 60;
        private const int BatchSize = 20;

        public EmailBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<EmailBackgroundWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailBackgroundWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EmailBackgroundWorker main loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("EmailBackgroundWorker stopped.");
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var pending = await db.EmailQueues
                .Where(e => !e.IsSent && e.RetryCount < MaxRetries)
                .OrderBy(e => e.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (!pending.Any()) return;

            _logger.LogInformation("Processing {Count} pending email(s).", pending.Count);

            foreach (var email in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var success = await emailService.SendEmailAsync(
                        email.ToEmail, email.ToName ?? "", email.Subject, email.Body);

                    if (success)
                    {
                        email.IsSent = true;
                        email.SentAt = DateTime.UtcNow;
                        email.ErrorMessage = null;
                    }
                    else
                    {
                        email.RetryCount++;
                        email.ErrorMessage = "SMTP not configured or send returned false.";
                    }
                }
                catch (Exception ex)
                {
                    email.RetryCount++;
                    email.ErrorMessage = ex.Message.Length > 1000
                        ? ex.Message.Substring(0, 1000)
                        : ex.Message;

                    _logger.LogWarning(ex, "Email send failed for {ToEmail} (attempt {Retry}/{Max})",
                        email.ToEmail, email.RetryCount, MaxRetries);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
