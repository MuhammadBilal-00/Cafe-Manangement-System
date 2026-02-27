using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cafe.Models;
using Cafe.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cafe.Interceptors
{
    /// <summary>
    /// EF Core interceptor that automatically creates AuditLog entries
    /// for every Insert, Update, and Delete that passes through SaveChanges.
    /// This is centralized — controllers never need to call audit manually.
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditSaveChangesInterceptor> _logger;

        // Entities we want to audit. Add new entity types here.
        private static readonly HashSet<Type> AuditedTypes = new()
        {
            typeof(Staff),
            typeof(Branch),
            typeof(Order),
            typeof(MenuItem),
            typeof(InventoryItem),
            typeof(Category),
            typeof(Feedback),
            typeof(Ingredient),
            typeof(StaffRole),
            typeof(Attendance),
            typeof(SalaryRecord),
            typeof(Expense),
            typeof(Customer),
            typeof(OrderItem),
            typeof(InventoryTransaction),
            typeof(DailySpecial),
            typeof(StaffSalary),
            typeof(StaffSchedule),
            typeof(SalaryAdjustment),
            typeof(SalaryPolicy),
        };

        // Track entries before SaveChanges so we can capture original values
        private List<AuditEntry>? _pendingAudits;

        public AuditSaveChangesInterceptor(
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditSaveChangesInterceptor> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // ── Async path ──
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
                _pendingAudits = CapturePendingAudits(eventData.Context);

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null && _pendingAudits is { Count: > 0 })
            {
                await WriteAuditLogs(eventData.Context, cancellationToken);
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        // ── Sync path (fallback) ──
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context is not null)
                _pendingAudits = CapturePendingAudits(eventData.Context);

            return base.SavingChanges(eventData, result);
        }

        public override int SavedChanges(
            SaveChangesCompletedEventData eventData,
            int result)
        {
            if (eventData.Context is not null && _pendingAudits is { Count: > 0 })
            {
                WriteAuditLogs(eventData.Context, CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
            }

            return base.SavedChanges(eventData, result);
        }

        // ── Capture changes BEFORE SaveChanges commits ──
        private List<AuditEntry> CapturePendingAudits(DbContext context)
        {
            var entries = new List<AuditEntry>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Skip AuditLog itself to avoid recursion
                if (entry.Entity is AuditLog)
                    continue;

                // Only audit types we care about
                if (!AuditedTypes.Contains(entry.Entity.GetType()))
                    continue;

                if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var audit = new AuditEntry
                {
                    EntityType = entry.Entity.GetType().Name,
                    State = entry.State,
                    Entry = entry,
                };

                // Capture primary key (works for Added after save too)
                var pkProp = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
                if (pkProp != null && entry.State != EntityState.Added)
                {
                    audit.EntityId = Convert.ToInt32(entry.CurrentValues[pkProp]);
                }
                audit.PrimaryKeyProperty = pkProp;

                // Capture BranchId if entity has it
                var branchProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "BranchId");
                if (branchProp?.CurrentValue is int bid)
                    audit.BranchId = bid;

                // Build change details
                switch (entry.State)
                {
                    case EntityState.Added:
                        audit.Action = "Create";
                        audit.Details = BuildCreateDetails(entry);
                        break;

                    case EntityState.Modified:
                        audit.Action = "Update";
                        audit.Details = BuildUpdateDetails(entry);
                        break;

                    case EntityState.Deleted:
                        audit.Action = "Delete";
                        audit.Details = BuildDeleteDetails(entry);
                        break;
                }

                // Detect soft-delete (IsActive set to false)
                if (entry.State == EntityState.Modified)
                {
                    var isActiveProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsActive");
                    if (isActiveProp != null
                        && isActiveProp.OriginalValue is true
                        && isActiveProp.CurrentValue is false)
                    {
                        audit.Action = "SoftDelete";
                    }

                    // Detect status changes (e.g., Order.Status, Feedback.Status)
                    var statusProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Status");
                    if (statusProp != null && !Equals(statusProp.OriginalValue, statusProp.CurrentValue))
                    {
                        audit.Action = "StatusChange";
                        audit.Details = $"Status changed from '{statusProp.OriginalValue}' to '{statusProp.CurrentValue}'";
                    }

                    // Detect PaymentStatus changes (SalaryRecord)
                    var paymentStatusProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "PaymentStatus");
                    if (paymentStatusProp != null && !Equals(paymentStatusProp.OriginalValue, paymentStatusProp.CurrentValue))
                    {
                        audit.Action = "StatusChange";
                        audit.Details = $"PaymentStatus changed from '{paymentStatusProp.OriginalValue}' to '{paymentStatusProp.CurrentValue}'";
                    }
                }

                entries.Add(audit);
            }

            return entries;
        }

        // ── Write captured audits after SaveChanges succeeds ──
        private async ValueTask WriteAuditLogs(DbContext context, CancellationToken ct)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userId = session?.GetUserId();
                var userName = session?.GetUserName();
                var userRole = session?.GetUserRole();
                var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

                // Auto-resolve branchId from session when not available from entity
                int? sessionBranchId = null;
                if (userRole == "BranchManager")
                    sessionBranchId = session?.GetManagedBranchId();
                else if (userRole == "Staff")
                    sessionBranchId = session?.GetStaffBranchId();

                foreach (var audit in _pendingAudits!)
                {
                    // For Added entities, get the generated PK now
                    int? entityId = audit.EntityId;
                    if (audit.State == EntityState.Added && audit.PrimaryKeyProperty != null)
                    {
                        entityId = Convert.ToInt32(audit.Entry.CurrentValues[audit.PrimaryKeyProperty]);
                    }

                    var log = new AuditLog
                    {
                        Action = audit.Action,
                        EntityType = audit.EntityType,
                        EntityId = entityId,
                        Details = audit.Details?.Length > 500 ? audit.Details[..500] : audit.Details,
                        UserId = userId,
                        UserName = userName,
                        UserRole = userRole,
                        BranchId = audit.BranchId ?? sessionBranchId,
                        IpAddress = ipAddress,
                        Timestamp = DateTime.UtcNow,
                    };

                    context.Set<AuditLog>().Add(log);
                }

                // Save audit logs — this SaveChanges call won't re-trigger the interceptor
                // because only AuditLog entities are added (filtered out in CapturePendingAudits)
                await context.SaveChangesAsync(ct);

                _pendingAudits = null;
            }
            catch (Exception ex)
            {
                // Audit logging must never crash the main operation
                _logger.LogError(ex, "Failed to write interceptor audit logs");
                _pendingAudits = null;
            }
        }

        // ── Detail Builders ──
        private static string BuildCreateDetails(EntityEntry entry)
        {
            var props = entry.Properties
                .Where(p => p.CurrentValue != null && !IsNavigationOrSensitive(p.Metadata.Name))
                .Select(p => $"{p.Metadata.Name}={FormatValue(p.CurrentValue)}")
                .Take(10);
            return "Created with: " + string.Join(", ", props);
        }

        private static string BuildUpdateDetails(EntityEntry entry)
        {
            var changes = entry.Properties
                .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)
                            && !IsNavigationOrSensitive(p.Metadata.Name))
                .Select(p => $"{p.Metadata.Name}: '{FormatValue(p.OriginalValue)}' → '{FormatValue(p.CurrentValue)}'")
                .Take(10);
            var detail = string.Join(", ", changes);
            return string.IsNullOrEmpty(detail) ? "No value changes detected" : detail;
        }

        private static string BuildDeleteDetails(EntityEntry entry)
        {
            var nameProp = entry.Properties.FirstOrDefault(p =>
                p.Metadata.Name is "Name" or "OrderNumber" or "RoleName" or "Email");
            return nameProp?.CurrentValue != null
                ? $"Deleted: {nameProp.CurrentValue}"
                : "Deleted entity";
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return "null";
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm");
            if (value is decimal dec) return dec.ToString("F2");
            var str = value.ToString() ?? "";
            return str.Length > 60 ? str[..60] + "…" : str;
        }

        private static bool IsNavigationOrSensitive(string name)
        {
            return name is "PasswordHash" or "CreatedDate" or "Id";
        }

        /// <summary>Temporary holder for audit data captured before SaveChanges.</summary>
        private sealed class AuditEntry
        {
            public string EntityType { get; set; } = "";
            public string Action { get; set; } = "";
            public int? EntityId { get; set; }
            public int? BranchId { get; set; }
            public string? Details { get; set; }
            public EntityState State { get; set; }
            public EntityEntry Entry { get; set; } = null!;
            public Microsoft.EntityFrameworkCore.Metadata.IProperty? PrimaryKeyProperty { get; set; }
        }
    }
}
