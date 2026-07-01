using System.Text.RegularExpressions;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    public class TenantProvisioningService : ITenantProvisioningService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuthService _auth;
        private readonly ITenantContext _tenant;
        private readonly ILogger<TenantProvisioningService> _logger;

        public TenantProvisioningService(
            ApplicationDbContext db, IAuthService auth, ITenantContext tenant,
            ILogger<TenantProvisioningService> logger)
        {
            _db = db;
            _auth = auth;
            _tenant = tenant;
            _logger = logger;
        }

        public static string Slugify(string input)
        {
            var slug = Regex.Replace((input ?? "").ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
            return slug.Length > 63 ? slug[..63] : slug;
        }

        public async Task<bool> IsSlugAvailableAsync(string slug)
        {
            slug = Slugify(slug);
            if (string.IsNullOrWhiteSpace(slug)) return false;
            using (_tenant.BypassFilter())
                return !await _db.Tenants.AnyAsync(t => t.Slug == slug);
        }

        public async Task<ProvisionResult> ProvisionAsync(ProvisionTenantRequest request)
        {
            var slug = Slugify(request.Slug);
            if (string.IsNullOrWhiteSpace(slug))
                return new ProvisionResult(false, null, null, "Please choose a valid workspace name.");

            // Cross-tenant uniqueness checks must bypass the tenant filter.
            using (_tenant.BypassFilter())
            {
                if (await _db.Tenants.AnyAsync(t => t.Slug == slug))
                    return new ProvisionResult(false, null, null, "That workspace address is already taken.");
                if (await _db.Users.AnyAsync(u => u.Email == request.AdminEmail))
                    return new ProvisionResult(false, null, null, "An account with that email already exists.");
            }

            // Run everything in one transaction so a partial tenant is never left behind.
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var freePlan = await GetOrCreateFreePlanAsync();

                // 1) Tenant
                var tenant = new Tenant
                {
                    Name = request.BusinessName.Trim(),
                    Slug = slug,
                    Status = "Trial",
                    PlanId = freePlan.Id,
                    BrandingJson = DefaultBrandingJson(request.BusinessName.Trim()),
                    CreatedAt = DateTime.UtcNow
                };
                _db.Tenants.Add(tenant);
                await _db.SaveChangesAsync();

                // From here, stamp everything for the new tenant automatically.
                _tenant.SetTenant(tenant.Id, isPlatformAdmin: false);

                // 2) Trial subscription on the Free plan
                _db.Subscriptions.Add(new Subscription
                {
                    TenantId = tenant.Id,
                    PlanId = freePlan.Id,
                    Status = "Trialing",
                    Provider = "Manual",
                    CurrentPeriodStart = DateTime.UtcNow,
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(14)
                });

                // 3) Admin (Tenant Admin = the "Owner" role, scoped to this tenant)
                var admin = new User
                {
                    Name = request.AdminName.Trim(),
                    Email = request.AdminEmail.Trim(),
                    Phone = string.IsNullOrWhiteSpace(request.AdminPhone) ? "N/A" : request.AdminPhone.Trim(),
                    Role = "Owner",
                    TenantId = tenant.Id,
                    PasswordHash = _auth.HashPassword(request.AdminPassword),
                    CreatedDate = DateTime.Now
                };
                _db.Users.Add(admin);
                await _db.SaveChangesAsync();

                // 4) Default branch (managed by the admin)
                var branch = new Branch
                {
                    Name = "Main Branch",
                    Location = "—",
                    ContactInfo = admin.Phone,
                    OpeningHours = "9:00 AM – 9:00 PM",
                    ManagerId = admin.Id,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };
                _db.Branches.Add(branch);

                // 5) Staff roles
                _db.StaffRoles.AddRange(
                    new StaffRole { RoleName = "Barista", Description = "Prepares coffee & beverages", DefaultHourlyRate = 0, DefaultMonthlySalary = 0, IsActive = true },
                    new StaffRole { RoleName = "Chef", Description = "Prepares food items", DefaultHourlyRate = 0, DefaultMonthlySalary = 0, IsActive = true },
                    new StaffRole { RoleName = "Cashier", Description = "Handles transactions", DefaultHourlyRate = 0, DefaultMonthlySalary = 0, IsActive = true },
                    new StaffRole { RoleName = "Waiter", Description = "Serves customers", DefaultHourlyRate = 0, DefaultMonthlySalary = 0, IsActive = true });

                // 6) Walk-In customer (needs a backing user; e-mail kept unique per tenant)
                var walkInUser = new User
                {
                    Name = "Walk-In Customer",
                    Email = $"walkin+{slug}@tenant.local",
                    Phone = "N/A",
                    Role = "Customer",
                    TenantId = tenant.Id,
                    CreatedDate = DateTime.Now
                };
                _db.Users.Add(walkInUser);
                await _db.SaveChangesAsync();
                _db.Customers.Add(new Customer
                {
                    UserId = walkInUser.Id,
                    LoyaltyPoints = 0,
                    JoinDate = DateTime.Now,
                    IsActive = true
                });

                // 7) Starter menu template
                await SeedStarterTemplateAsync(request.Template, branch);

                // 8) Welcome email (processed by the existing background worker)
                _db.EmailQueues.Add(new EmailQueue
                {
                    ToEmail = admin.Email,
                    ToName = admin.Name,
                    Subject = $"Welcome to {tenant.Name} on Cafe Manager 🎉",
                    Body = $"<p>Hi {admin.Name},</p><p>Your workspace <b>{tenant.Name}</b> is ready. " +
                           $"Sign in and finish setup to start taking orders.</p>",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Provisioned tenant {Slug} (id {Id})", tenant.Slug, tenant.Id);
                return new ProvisionResult(true, tenant, admin, null);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Tenant provisioning failed for slug {Slug}", slug);
                return new ProvisionResult(false, null, null, "Something went wrong creating your workspace. Please try again.");
            }
        }

        private async Task<Plan> GetOrCreateFreePlanAsync()
        {
            Plan? plan;
            using (_tenant.BypassFilter())
                plan = await _db.Plans.OrderBy(p => p.SortOrder).FirstOrDefaultAsync(p => p.PriceMonthly == 0 && p.IsActive);

            if (plan != null) return plan;

            plan = new Plan
            {
                Name = "Free",
                Description = "Get started — core POS for a single branch.",
                PriceMonthly = 0,
                MaxBranches = 1,
                MaxUsers = 5,
                Features = $"{FeatureCatalog.Invoicing}",
                IsActive = true,
                SortOrder = 0
            };
            _db.Plans.Add(plan);
            await _db.SaveChangesAsync();
            return plan;
        }

        private async Task SeedStarterTemplateAsync(string template, Branch branch)
        {
            (string cat, string item, decimal price, decimal cost)[] rows = (template?.ToLowerInvariant()) switch
            {
                "bakery" => new[]
                {
                    ("Bakery", "Croissant", 220m, 70m),
                    ("Bakery", "Chocolate Muffin", 250m, 80m),
                    ("Bakery", "Sourdough Loaf", 450m, 150m),
                    ("Coffee", "Espresso", 250m, 55m),
                    ("Coffee", "Cappuccino", 380m, 90m),
                },
                "restaurant" => new[]
                {
                    ("Starters", "Garlic Bread", 350m, 110m),
                    ("Mains", "Grilled Chicken", 950m, 320m),
                    ("Mains", "Beef Burger", 780m, 260m),
                    ("Desserts", "Chocolate Cake", 480m, 130m),
                    ("Beverages", "Fresh Lime", 200m, 50m),
                },
                _ => new[] // cafe (default)
                {
                    ("Coffee", "Espresso", 250m, 55m),
                    ("Coffee", "Cappuccino", 380m, 90m),
                    ("Coffee", "Caramel Latte", 450m, 110m),
                    ("Tea", "Masala Chai", 200m, 45m),
                    ("Food", "Club Sandwich", 680m, 200m),
                    ("Desserts", "Cheesecake", 450m, 120m),
                },
            };

            var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in rows.Select(r => r.cat).Distinct())
            {
                var c = new Category { Name = name, Description = $"{name} menu", IsActive = true, CreatedDate = DateTime.Now };
                categories[name] = c;
                _db.Categories.Add(c);
            }
            await _db.SaveChangesAsync();

            foreach (var (cat, item, price, cost) in rows)
            {
                _db.MenuItems.Add(new MenuItem
                {
                    Name = item,
                    Description = item,
                    Price = price,
                    CostPrice = cost,
                    CategoryId = categories[cat].Id,
                    BranchId = branch.Id,
                    Availability = true,
                    PreparationTime = 10,
                    IsVegetarian = true,
                    CreatedDate = DateTime.Now
                });
            }
            await _db.SaveChangesAsync();
        }

        public static string DefaultBrandingJson(string businessName) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                businessName,
                logoUrl = (string?)null,
                primaryColor = "#d4af37",
                sidebarColor = "#1e2a3a",
                receiptHeader = businessName,
                receiptFooter = "Thank you for visiting!"
            });
    }
}
