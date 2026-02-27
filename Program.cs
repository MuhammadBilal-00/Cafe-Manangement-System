using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Cafe.Data;
using Cafe.Services;
using Cafe.Middleware;
using Cafe.Interceptors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Register audit interceptor as scoped (needs IHttpContextAccessor per-request)
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// Add Entity Framework with audit interceptor
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
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
builder.Services.AddHttpContextAccessor();

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
app.UseMiddleware<AuthenticationMiddleware>();

app.UseAuthorization();

// Configure routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
