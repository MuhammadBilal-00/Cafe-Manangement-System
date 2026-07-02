using Microsoft.EntityFrameworkCore;
using Cafe.Data;
using Cafe.Services;
using Cafe.Middleware;
using Cafe.Interceptors;
using Cafe.Hubs;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community license (free for orgs under $1M revenue) — required before any PDF render.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container
// #56 i18n: localization + view localization. Resource .resx files live under /Resources.
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews().AddViewLocalization();

// Supported cultures — English (default) and Urdu (RTL). Culture is chosen via cookie.
var supportedCultures = new[] { "en", "ur" };
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

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

// ── Phase 3: inventory & supply chain ──
builder.Services.AddScoped<ISupplyChainService, SupplyChainService>();

// ── Phase 4: receivables/payables ──
builder.Services.AddScoped<IReceivablesService, ReceivablesService>();

// ── Phase 9: reusable export (CSV/Excel) ──
builder.Services.AddScoped<IExportService, ExportService>();

// ── Phase 6: customer portal & marketing ──
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IGiftCardService, GiftCardService>();
builder.Services.AddScoped<ISmsProvider, LoggingSmsProvider>();
builder.Services.AddScoped<ISmsQueueService, SmsQueueService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
builder.Services.AddScoped<IKotPrintService, KotPrintService>();

// ── Phase 5: accounting + pluggable tax e-invoicing ──
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<Cafe.Services.TaxInvoice.PakFbrTaxInvoiceProvider>();
builder.Services.AddScoped<Cafe.Services.TaxInvoice.ITaxInvoiceProvider, Cafe.Services.TaxInvoice.NullTaxInvoiceProvider>();

// ── SaaS platform services (Phase 0): feature gating, billing, provisioning, branding ──
builder.Services.AddScoped<IFeatureGate, FeatureGate>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<ITenantBrandingService, TenantBrandingService>();
builder.Services.AddScoped<Cafe.Services.Billing.ManualBillingProvider>();
builder.Services.AddScoped<Cafe.Services.Billing.StripeBillingProvider>();
builder.Services.AddScoped<Cafe.Services.Billing.IBillingProvider>(sp =>
    sp.GetRequiredService<Cafe.Services.Billing.ManualBillingProvider>());

builder.Services.AddHostedService<EmailBackgroundWorker>();
builder.Services.AddHostedService<SmsBackgroundWorker>();
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

// Apply pending migrations automatically. Migrations are authoritative: if one fails,
// startup fails loudly. (The old 2714 "reconciliation" loop could mark a migration as
// applied without creating its tables, which broke seeding with missing-object errors.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
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

// #56 i18n: apply the request culture (cookie → Accept-Language → default) before routing.
app.UseRequestLocalization(app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>>().Value);

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
