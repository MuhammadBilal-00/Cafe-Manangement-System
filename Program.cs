using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Cafe.Data;
using Cafe.Services;
using Cafe.Middleware;
using Cafe.Interceptors;
using Cafe.Hubs;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community license (free for orgs under $1M revenue) — required before any PDF render.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container
builder.Services.AddControllersWithViews();

// AJAX/JSON POST actions ([FromBody]) send the antiforgery token via this header
// instead of a form field — without naming it here, [ValidateAntiForgeryToken]
// only ever checks form data and silently rejects every JSON request that carries
// the token in a header (e.g. Order/Salary create & status-update endpoints).
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// ── Multi-tenancy (Phase 0): per-request tenant scope + auto-stamping interceptor ──
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantStampingInterceptor>();

// Register audit interceptor as scoped (needs IHttpContextAccessor per-request)
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// Add Entity Framework with tenant-stamping (first) + audit interceptors
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    // Order matters: stamp TenantId before the audit interceptor captures/writes rows.
    options.AddInterceptors(serviceProvider.GetRequiredService<TenantStampingInterceptor>());
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
    options.ConfigureWarnings(w => w
        .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)
        // Required-navigation + global-filter interaction is intentional and consistent here.
        .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});

// Add authentication services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ISalaryCalculationService, SalaryCalculationService>();
builder.Services.AddScoped<ISalaryPolicyService, SalaryPolicyService>();
builder.Services.AddScoped<ISalaryService, SalaryService>();
builder.Services.AddScoped<IFinancialService, FinancialService>();
builder.Services.AddScoped<IMenuReportService, MenuReportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Checkout modules: promos, bank partnerships, per-branch settings, invoicing + PDF
builder.Services.AddScoped<IBranchSettingService, BranchSettingService>();
builder.Services.AddScoped<ICheckoutPricingService, CheckoutPricingService>();
builder.Services.AddScoped<IPdfInvoiceService, PdfInvoiceService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// ── Phase 1: POS & restaurant core ──
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<IKitchenService, KitchenService>();
builder.Services.AddScoped<IPosService, PosService>();

// ── SaaS platform services (Phase 0): feature gating, billing, provisioning, branding ──
builder.Services.AddScoped<IFeatureGate, FeatureGate>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<ITenantBrandingService, TenantBrandingService>();
builder.Services.AddScoped<Cafe.Services.Billing.ManualBillingProvider>();
builder.Services.AddScoped<Cafe.Services.Billing.StripeBillingProvider>();
builder.Services.AddScoped<Cafe.Services.Billing.IBillingProvider>(sp =>
    sp.GetRequiredService<Cafe.Services.Billing.ManualBillingProvider>());

builder.Services.AddHostedService<EmailBackgroundWorker>();
builder.Services.AddHttpContextAccessor();

// Add SignalR for real-time notifications
builder.Services.AddSignalR();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "CafeManagement.Session";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Add memory cache and logging
builder.Services.AddMemoryCache();
builder.Services.AddLogging();

var app = builder.Build();

// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2714) // 2714 = "There is already an object named '...' in the database"
    {
        // This occurs when the database was created before EF Core migrations were introduced.
        logger.LogWarning(ex, "Database objects already exist. Reconciling migration history...");

        // Ensure __EFMigrationsHistory table exists
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
            CREATE TABLE [__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            )");

        // Apply each pending migration individually: migrations that create objects
        // already in the database are marked as applied, while genuinely new migrations
        // (e.g. AddAuditLogsTable) are executed normally.
        var efVersion = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(DbContext).Assembly)
            ?.InformationalVersion?.Split('+')[0] ?? "9.0.0";
        var pending = db.Database.GetPendingMigrations().ToList();
        var migrator = db.GetInfrastructure()
            .GetRequiredService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        var reconciled = 0;

        foreach (var migration in pending)
        {
            try
            {
                migrator.Migrate(migration);
            }
            catch (Microsoft.Data.SqlClient.SqlException mex) when (mex.Number == 2714)
            {
                // This migration creates objects that already exist, mark as applied
                db.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {0}) " +
                    "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, {1})",
                    migration, efVersion);
                reconciled++;
            }
        }

        logger.LogInformation("Reconciled {Count} migrations with existing database schema.", reconciled);
    }
}

// Seed demo data
await Cafe.Data.SeedData.InitializeAsync(app.Services);

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Use session before custom middleware
app.UseSession();

// Add custom authentication middleware
app.UseMiddleware<GlobalExceptionMiddleware>();
// Resolve the tenant BEFORE the auth gate so all data access is isolated from the first query.
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();

app.UseAuthorization();

// Configure routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<Cafe.Hubs.KitchenHub>("/hubs/kitchen");

app.Run();
