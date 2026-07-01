using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cafe.Models;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cafe.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var tenantCtx   = scope.ServiceProvider.GetRequiredService<ITenantContext>();

            // ── Platform seeding (idempotent, runs on every startup for fresh AND existing DBs) ──
            await EnsurePlansAsync(context);
            await EnsurePlatformAdminAsync(context, authService);
            await EnsureTenantsHavePlanAsync(context); // any plan-less tenant (e.g. the migration's Demo) → Pro

            // If real tenant data already exists, don't seed demo data.
            if (await context.Users.AnyAsync(u => u.Role != "PlatformAdmin"))
                return;

            // ── Fresh DB: create the Demo tenant and seed all demo data under it ──
            var proPlan = await context.Plans.FirstAsync(p => p.Name == "Pro");
            var demoTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo");
            if (demoTenant == null)
            {
                demoTenant = new Tenant
                {
                    Name = "Demo Cafe Co.",
                    Slug = "demo",
                    Status = "Active",
                    PlanId = proPlan.Id,
                    BrandingJson = TenantProvisioningService.DefaultBrandingJson("Demo Cafe Co."),
                    CreatedAt = DateTime.UtcNow
                };
                context.Tenants.Add(demoTenant);
                await context.SaveChangesAsync();
            }

            // From here every insert is auto-stamped with the Demo tenant id.
            tenantCtx.SetTenant(demoTenant.Id, isPlatformAdmin: false);

            var rng = new Random(42);

            // ── Users ─────────────────────────────────────────────────────────────────
            var owner = new User { Name = "John Owner", Email = "admin@cafe.com", Phone = "555-0100", Role = "Owner",
                PasswordHash = authService.HashPassword("admin123"), CreatedDate = DateTime.Now.AddMonths(-12) };

            var mgr1 = new User { Name = "Sarah Johnson", Email = "sarah@cafe.com",   Phone = "555-0101", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-11) };
            var mgr2 = new User { Name = "Michael Chen",  Email = "michael@cafe.com", Phone = "555-0102", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-10) };
            var mgr3 = new User { Name = "Priya Patel",   Email = "priya@cafe.com",   Phone = "555-0103", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-9) };
            var mgr4 = new User { Name = "David Park",    Email = "david@cafe.com",   Phone = "555-0104", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-8) };

            var staffUsers = new[]
            {
                new User { Name = "Emily Davis",    Email = "emily@cafe.com",   Phone = "555-0201", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-10) },
                new User { Name = "James Wilson",   Email = "james@cafe.com",   Phone = "555-0202", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-9)  },
                new User { Name = "Aisha Hassan",   Email = "aisha@cafe.com",   Phone = "555-0203", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-8)  },
                new User { Name = "Carlos Rivera",  Email = "carlos@cafe.com",  Phone = "555-0204", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-8)  },
                new User { Name = "Nina Kowalski",  Email = "nina@cafe.com",    Phone = "555-0205", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-7)  },
                new User { Name = "Omar Abdullah",  Email = "omar@cafe.com",    Phone = "555-0206", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-7)  },
                new User { Name = "Sophie Laurent", Email = "sophie@cafe.com",  Phone = "555-0207", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-6)  },
                new User { Name = "Ravi Sharma",    Email = "ravi@cafe.com",    Phone = "555-0208", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-6)  },
                new User { Name = "Fatima Malik",   Email = "fatima@cafe.com",  Phone = "555-0209", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-5)  },
                new User { Name = "Leo Nguyen",     Email = "leo@cafe.com",     Phone = "555-0210", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-5)  },
                new User { Name = "Maya Petrova",   Email = "maya@cafe.com",    Phone = "555-0211", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-4)  },
                new User { Name = "Tariq Ahmed",    Email = "tariq@cafe.com",   Phone = "555-0212", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-4)  },
            };

            var custUsers = new[]
            {
                new User { Name = "Alice Brown",   Email = "alice@example.com",  Phone = "555-0301", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-8) },
                new User { Name = "Bob Martinez",  Email = "bob@example.com",    Phone = "555-0302", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-6) },
                new User { Name = "Carol Kim",     Email = "carol@example.com",  Phone = "555-0303", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-5) },
                new User { Name = "Daniel Smith",  Email = "daniel@example.com", Phone = "555-0304", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-4) },
                new User { Name = "Eva Green",     Email = "eva@example.com",    Phone = "555-0305", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-3) },
            };

            context.Users.Add(owner);
            context.Users.AddRange(mgr1, mgr2, mgr3, mgr4);
            context.Users.AddRange(staffUsers);
            context.Users.AddRange(custUsers);
            await context.SaveChangesAsync();

            // ── Branches ──────────────────────────────────────────────────────────────
            var br1 = new Branch { Name = "Downtown Cafe",    Location = "123 Main Street",      ContactInfo = "555-1001", OpeningHours = "7:00 AM – 10:00 PM", ManagerId = mgr1.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-11) };
            var br2 = new Branch { Name = "Uptown Bistro",    Location = "456 Oak Avenue",        ContactInfo = "555-1002", OpeningHours = "8:00 AM – 9:00 PM",  ManagerId = mgr2.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-10) };
            var br3 = new Branch { Name = "Suburb Mall Cafe", Location = "789 Greenfield Mall",   ContactInfo = "555-1003", OpeningHours = "9:00 AM – 9:00 PM",  ManagerId = mgr3.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-7) };
            var br4 = new Branch { Name = "City Center Hub",  Location = "1 Business District",   ContactInfo = "555-1004", OpeningHours = "6:30 AM – 11:00 PM", ManagerId = mgr4.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-5) };

            context.Branches.AddRange(br1, br2, br3, br4);
            await context.SaveChangesAsync();

            // ── Staff Roles ──────────────────────────────────────────────────────────
            var rBarista = new StaffRole { RoleName = "Barista",  Description = "Prepares coffee & beverages", DefaultHourlyRate = 15m, DefaultMonthlySalary = 28000m, IsActive = true, IsSystemRole = false };
            var rChef    = new StaffRole { RoleName = "Chef",     Description = "Prepares food items",         DefaultHourlyRate = 22m, DefaultMonthlySalary = 38000m, IsActive = true, IsSystemRole = false };
            var rCashier = new StaffRole { RoleName = "Cashier",  Description = "Handles transactions",        DefaultHourlyRate = 13m, DefaultMonthlySalary = 22000m, IsActive = true, IsSystemRole = false };
            var rWaiter  = new StaffRole { RoleName = "Waiter",   Description = "Serves customers",            DefaultHourlyRate = 12m, DefaultMonthlySalary = 20000m, IsActive = true, IsSystemRole = false };

            context.StaffRoles.AddRange(rBarista, rChef, rCashier, rWaiter);
            await context.SaveChangesAsync();

            // ── Staff Records (3 per branch = 12 total) ──────────────────────────────
            var staffRecs = new[]
            {
                new Staff { UserId = staffUsers[0].Id,  StaffRoleId = rBarista.Id, BranchId = br1.Id, HireDate = DateTime.Now.AddMonths(-10), EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Beverages", EmployeeId = "EMP001", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[1].Id,  StaffRoleId = rChef.Id,    BranchId = br1.Id, HireDate = DateTime.Now.AddMonths(-9),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Kitchen",   EmployeeId = "EMP002", PerformanceRating = 5, IsActive = true },
                new Staff { UserId = staffUsers[2].Id,  StaffRoleId = rCashier.Id, BranchId = br1.Id, HireDate = DateTime.Now.AddMonths(-8),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Counter",   EmployeeId = "EMP003", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[3].Id,  StaffRoleId = rBarista.Id, BranchId = br2.Id, HireDate = DateTime.Now.AddMonths(-8),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Beverages", EmployeeId = "EMP004", PerformanceRating = 5, IsActive = true },
                new Staff { UserId = staffUsers[4].Id,  StaffRoleId = rWaiter.Id,  BranchId = br2.Id, HireDate = DateTime.Now.AddMonths(-7),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Service",   EmployeeId = "EMP005", PerformanceRating = 3, IsActive = true },
                new Staff { UserId = staffUsers[5].Id,  StaffRoleId = rChef.Id,    BranchId = br2.Id, HireDate = DateTime.Now.AddMonths(-7),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Kitchen",   EmployeeId = "EMP006", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[6].Id,  StaffRoleId = rBarista.Id, BranchId = br3.Id, HireDate = DateTime.Now.AddMonths(-6),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Beverages", EmployeeId = "EMP007", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[7].Id,  StaffRoleId = rCashier.Id, BranchId = br3.Id, HireDate = DateTime.Now.AddMonths(-6),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Counter",   EmployeeId = "EMP008", PerformanceRating = 5, IsActive = true },
                new Staff { UserId = staffUsers[8].Id,  StaffRoleId = rWaiter.Id,  BranchId = br3.Id, HireDate = DateTime.Now.AddMonths(-5),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Service",   EmployeeId = "EMP009", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[9].Id,  StaffRoleId = rChef.Id,    BranchId = br4.Id, HireDate = DateTime.Now.AddMonths(-4),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Kitchen",   EmployeeId = "EMP010", PerformanceRating = 5, IsActive = true },
                new Staff { UserId = staffUsers[10].Id, StaffRoleId = rBarista.Id, BranchId = br4.Id, HireDate = DateTime.Now.AddMonths(-4),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Beverages", EmployeeId = "EMP011", PerformanceRating = 4, IsActive = true },
                new Staff { UserId = staffUsers[11].Id, StaffRoleId = rCashier.Id, BranchId = br4.Id, HireDate = DateTime.Now.AddMonths(-3),  EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Counter",   EmployeeId = "EMP012", PerformanceRating = 3, IsActive = true },
            };
            context.Staff.AddRange(staffRecs);
            await context.SaveChangesAsync();

            // ── Customers ─────────────────────────────────────────────────────────────
            context.Customers.AddRange(
                new Customer { UserId = custUsers[0].Id, LoyaltyPoints = 340, JoinDate = DateTime.Now.AddMonths(-8), IsActive = true },
                new Customer { UserId = custUsers[1].Id, LoyaltyPoints = 215, JoinDate = DateTime.Now.AddMonths(-6), IsActive = true },
                new Customer { UserId = custUsers[2].Id, LoyaltyPoints = 130, JoinDate = DateTime.Now.AddMonths(-5), IsActive = true },
                new Customer { UserId = custUsers[3].Id, LoyaltyPoints = 80,  JoinDate = DateTime.Now.AddMonths(-4), IsActive = true },
                new Customer { UserId = custUsers[4].Id, LoyaltyPoints = 45,  JoinDate = DateTime.Now.AddMonths(-3), IsActive = true }
            );
            await context.SaveChangesAsync();

            // ── Categories ────────────────────────────────────────────────────────────
            var catCoffee   = new Category { Name = "Coffee",    Description = "Hot and cold coffee beverages", IsActive = true, CreatedDate = DateTime.Now.AddMonths(-11) };
            var catTea      = new Category { Name = "Tea",       Description = "Premium teas and infusions",    IsActive = true, CreatedDate = DateTime.Now.AddMonths(-11) };
            var catFood     = new Category { Name = "Food",      Description = "Sandwiches, wraps and meals",   IsActive = true, CreatedDate = DateTime.Now.AddMonths(-11) };
            var catDessert  = new Category { Name = "Desserts",  Description = "Sweet treats and cakes",        IsActive = true, CreatedDate = DateTime.Now.AddMonths(-11) };
            var catSmoothie = new Category { Name = "Smoothies", Description = "Fresh blended fruit drinks",    IsActive = true, CreatedDate = DateTime.Now.AddMonths(-10) };

            context.Categories.AddRange(catCoffee, catTea, catFood, catDessert, catSmoothie);
            await context.SaveChangesAsync();

            // ── Menu Items (with Unsplash images) ─────────────────────────────────────
            var menuItems = new[]
            {
                // Coffee
                new MenuItem { Name = "Espresso",      Description = "Rich and bold single espresso shot",           Price = 250m, CostPrice = 55m,  CategoryId = catCoffee.Id,   BranchId = br1.Id, Availability = true, PreparationTime = 3,  Calories = 5,   IsVegetarian = true, IsVegan = true,  IsFeatured = true,  PopularityScore = 88, ImageUrl = "https://images.unsplash.com/photo-1510591509098-f4fdc6d0ff04?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Cappuccino",    Description = "Classic espresso with velvety steamed foam",    Price = 380m, CostPrice = 90m,  CategoryId = catCoffee.Id,   BranchId = br1.Id, Availability = true, PreparationTime = 5,  Calories = 80,  IsVegetarian = true,                  IsFeatured = true,  PopularityScore = 95, ImageUrl = "https://images.unsplash.com/photo-1572442388796-11668a67e53d?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Caramel Latte", Description = "Smooth latte with rich caramel drizzle",        Price = 450m, CostPrice = 110m, CategoryId = catCoffee.Id,   BranchId = br1.Id, Availability = true, PreparationTime = 6,  Calories = 150, IsVegetarian = true,                                      PopularityScore = 92, ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Cold Brew",     Description = "Slow-steeped cold brew, served over ice",       Price = 420m, CostPrice = 100m, CategoryId = catCoffee.Id,   BranchId = br2.Id, Availability = true, PreparationTime = 2,  Calories = 15,  IsVegetarian = true, IsVegan = true,                       PopularityScore = 87, ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?w=400&h=200&fit=crop" },
                // Tea
                new MenuItem { Name = "Masala Chai",  Description = "Traditional spiced milk tea blend",              Price = 200m, CostPrice = 45m,  CategoryId = catTea.Id,      BranchId = br2.Id, Availability = true, PreparationTime = 5,  Calories = 90,  IsVegetarian = true, IsSpicy = true,                       PopularityScore = 78, ImageUrl = "https://images.unsplash.com/photo-1556679343-c7306c1976bc?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Matcha Latte", Description = "Japanese green tea with oat milk",               Price = 390m, CostPrice = 95m,  CategoryId = catTea.Id,      BranchId = br3.Id, Availability = true, PreparationTime = 5,  Calories = 110, IsVegetarian = true, IsGlutenFree = true, IsFeatured = true, PopularityScore = 83, ImageUrl = "https://images.unsplash.com/photo-1515823662972-da6a2e4d3002?w=400&h=200&fit=crop" },
                // Food
                new MenuItem { Name = "Club Sandwich", Description = "Triple-decker with grilled chicken & bacon",    Price = 680m, CostPrice = 200m, CategoryId = catFood.Id,     BranchId = br1.Id, Availability = true, PreparationTime = 15, Calories = 520,                                       IsFeatured = true,  PopularityScore = 96, ImageUrl = "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Caesar Salad",  Description = "Fresh romaine, parmesan, croutons & dressing",  Price = 580m, CostPrice = 160m, CategoryId = catFood.Id,     BranchId = br2.Id, Availability = true, PreparationTime = 10, Calories = 380, IsVegetarian = true,                                      PopularityScore = 80, ImageUrl = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Pancake Stack", Description = "Fluffy buttermilk pancakes with maple syrup",   Price = 520m, CostPrice = 140m, CategoryId = catFood.Id,     BranchId = br3.Id, Availability = true, PreparationTime = 12, Calories = 480, IsVegetarian = true,                  IsFeatured = true,  PopularityScore = 89, ImageUrl = "https://images.unsplash.com/photo-1554520735-0a6b8b6ce8b7?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Avocado Toast", Description = "Sourdough with smashed avocado & poached egg",  Price = 550m, CostPrice = 150m, CategoryId = catFood.Id,     BranchId = br4.Id, Availability = true, PreparationTime = 10, Calories = 340, IsVegetarian = true,                                      PopularityScore = 85, ImageUrl = "https://images.unsplash.com/photo-1541519227354-08fa5d50c820?w=400&h=200&fit=crop" },
                // Desserts
                new MenuItem { Name = "Chocolate Cake", Description = "Rich dark chocolate layer cake",               Price = 480m, CostPrice = 130m, CategoryId = catDessert.Id,  BranchId = br1.Id, Availability = true, PreparationTime = 5,  Calories = 420, IsVegetarian = true,                  IsFeatured = true,  PopularityScore = 93, ImageUrl = "https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=400&h=200&fit=crop" },
                new MenuItem { Name = "NY Cheesecake",  Description = "Creamy New York style baked cheesecake",       Price = 450m, CostPrice = 120m, CategoryId = catDessert.Id,  BranchId = br2.Id, Availability = true, PreparationTime = 3,  Calories = 380, IsVegetarian = true,                                      PopularityScore = 82, ImageUrl = "https://images.unsplash.com/photo-1533134242443-d4fd215305ad?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Fudge Brownie",  Description = "Warm gooey chocolate brownie with ice cream",  Price = 380m, CostPrice = 95m,  CategoryId = catDessert.Id,  BranchId = br3.Id, Availability = true, PreparationTime = 5,  Calories = 360, IsVegetarian = true,                                      PopularityScore = 76, ImageUrl = "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?w=400&h=200&fit=crop" },
                // Smoothies
                new MenuItem { Name = "Berry Blast",    Description = "Strawberry, blueberry & raspberry blend",      Price = 420m, CostPrice = 100m, CategoryId = catSmoothie.Id, BranchId = br4.Id, Availability = true, PreparationTime = 5,  Calories = 220, IsVegetarian = true, IsVegan = true, IsGlutenFree = true, PopularityScore = 79, ImageUrl = "https://images.unsplash.com/photo-1553530666-ba11a7da3888?w=400&h=200&fit=crop" },
                new MenuItem { Name = "Mango Tango",    Description = "Fresh mango, banana & coconut milk blend",     Price = 400m, CostPrice = 95m,  CategoryId = catSmoothie.Id, BranchId = br2.Id, Availability = true, PreparationTime = 5,  Calories = 250, IsVegetarian = true, IsVegan = true, IsGlutenFree = true, PopularityScore = 74, ImageUrl = "https://images.unsplash.com/photo-1546173159-315724a31696?w=400&h=200&fit=crop" },
            };
            context.MenuItems.AddRange(menuItems);
            await context.SaveChangesAsync();

            // ── Suppliers ─────────────────────────────────────────────────────────────
            var sup1 = new Supplier { Name = "Fresh Foods Co.",    ContactPerson = "Ali Khan",   Phone = "555-2001", Email = "ali@freshfoods.com",   BranchId = br1.Id, IsActive = true };
            var sup2 = new Supplier { Name = "Meat Masters Ltd.",  ContactPerson = "Zara Ahmed", Phone = "555-2002", Email = "zara@meatmasters.com", BranchId = br1.Id, IsActive = true };
            var sup3 = new Supplier { Name = "Coffee Central",     ContactPerson = "Sam Rivera", Phone = "555-2003", Email = "sam@coffeecentral.com",BranchId = br2.Id, IsActive = true };
            var sup4 = new Supplier { Name = "Dairy Delights",     ContactPerson = "Nadia Bibi", Phone = "555-2004", Email = "nadia@dairy.com",      BranchId = br2.Id, IsActive = true };
            context.Suppliers.AddRange(sup1, sup2, sup3, sup4);
            await context.SaveChangesAsync();

            // ── Inventory Items ───────────────────────────────────────────────────────
            var invItems = new[]
            {
                new InventoryItem { Name = "Coffee Beans",    Quantity = 50, Unit = "kg",     BranchId = br1.Id, UnitPrice = 1200m, ReorderLevel = 10, SupplierId = sup3.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Whole Milk",      Quantity = 40, Unit = "L",      BranchId = br1.Id, UnitPrice = 180m,  ReorderLevel = 20, SupplierId = sup4.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Cane Sugar",      Quantity = 20, Unit = "kg",     BranchId = br1.Id, UnitPrice = 120m,  ReorderLevel = 8,  LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Chicken Breast",  Quantity = 15, Unit = "kg",     BranchId = br1.Id, UnitPrice = 900m,  ReorderLevel = 5,  SupplierId = sup2.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Salad Leaves",    Quantity = 8,  Unit = "kg",     BranchId = br2.Id, UnitPrice = 400m,  ReorderLevel = 3,  SupplierId = sup1.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Coffee Beans",    Quantity = 35, Unit = "kg",     BranchId = br2.Id, UnitPrice = 1200m, ReorderLevel = 10, SupplierId = sup3.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Oat Milk",        Quantity = 25, Unit = "L",      BranchId = br3.Id, UnitPrice = 280m,  ReorderLevel = 10, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Cocoa Powder",    Quantity = 12, Unit = "kg",     BranchId = br3.Id, UnitPrice = 800m,  ReorderLevel = 4,  LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Fresh Fruits",    Quantity = 20, Unit = "kg",     BranchId = br4.Id, UnitPrice = 350m,  ReorderLevel = 8,  SupplierId = sup1.Id, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Sourdough Bread", Quantity = 4,  Unit = "loaves", BranchId = br4.Id, UnitPrice = 250m,  ReorderLevel = 10, LastUpdated = DateTime.Now },
            };
            context.InventoryItems.AddRange(invItems);
            await context.SaveChangesAsync();

            // ── Orders (4–9 per day per branch, last 90 days) ────────────────────────
            var branches  = new[] { br1, br2, br3, br4 };
            var custIds   = custUsers.Select(c => c.Id).ToArray();
            var orderNum  = 1001;
            var ordersToAdd    = new List<Order>();
            var orderItemsToAdd = new List<OrderItem>();

            for (int daysAgo = 90; daysAgo >= 0; daysAgo--)
            {
                var date = DateTime.Now.Date.AddDays(-daysAgo);
                if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                foreach (var br in branches)
                {
                    // Newer branches have fewer historical days
                    int openDaysAgo = (int)(DateTime.Now - br.CreatedDate).TotalDays;
                    if (daysAgo > openDaysAgo) continue;

                    int ordersToday = rng.Next(4, 10);
                    for (int i = 0; i < ordersToday; i++)
                    {
                        var item1  = menuItems[rng.Next(menuItems.Length)];
                        var qty1   = rng.Next(1, 4);
                        var total  = item1.Price * qty1;
                        var status = daysAgo > 1 ? "Completed" : (rng.Next(5) == 0 ? "Pending" : "Completed");

                        var order = new Order
                        {
                            OrderNumber = $"ORD-{orderNum++}",
                            CustomerId  = custIds[rng.Next(custIds.Length)],
                            BranchId    = br.Id,
                            OrderDate   = date.AddHours(rng.Next(7, 22)).AddMinutes(rng.Next(60)),
                            Status      = status,
                            TotalAmount = total,
                        };

                        // Occasionally add a second item
                        MenuItem? item2 = null;
                        if (rng.Next(3) == 0)
                        {
                            item2 = menuItems[rng.Next(menuItems.Length)];
                            var qty2 = rng.Next(1, 3);
                            order.TotalAmount += item2.Price * qty2;
                        }

                        ordersToAdd.Add(order);
                        orderItemsToAdd.Add(new OrderItem { Order = order, MenuItemId = item1.Id, Quantity = qty1, Price = item1.Price });
                        if (item2 != null)
                            orderItemsToAdd.Add(new OrderItem { Order = order, MenuItemId = item2.Id, Quantity = 1, Price = item2.Price });
                    }
                }

                if (ordersToAdd.Count >= 150)
                {
                    context.Orders.AddRange(ordersToAdd);
                    context.OrderItems.AddRange(orderItemsToAdd);
                    await context.SaveChangesAsync();
                    ordersToAdd.Clear();
                    orderItemsToAdd.Clear();
                }
            }
            if (ordersToAdd.Any())
            {
                context.Orders.AddRange(ordersToAdd);
                context.OrderItems.AddRange(orderItemsToAdd);
                await context.SaveChangesAsync();
            }

            // ── Expenses (7 months × 4 branches × 6 categories) ──────────────────────
            var expCategories = new[]
            {
                // (category,  base amount,  variance)
                ("Rent",        48000m, 0m),
                ("Utilities",   12000m, 3000m),
                ("Supplies",    18000m, 5000m),
                ("Maintenance",  7000m, 2500m),
                ("Marketing",    5500m, 1500m),
                ("Bills",        4500m, 1200m),
            };

            var expenseList = new List<Expense>();
            for (int m = 7; m >= 0; m--)
            {
                var period = DateTime.Now.AddMonths(-m);
                foreach (var br in branches)
                {
                    decimal brMultiplier = br == br1 ? 1.0m : br == br2 ? 0.92m : br == br3 ? 0.80m : 0.70m;
                    foreach (var (cat, baseAmt, variance) in expCategories)
                    {
                        decimal amt = (baseAmt + (decimal)(rng.NextDouble() * (double)variance * 2 - (double)variance)) * brMultiplier;
                        if (amt < 1000m) amt = 1000m;

                        expenseList.Add(new Expense
                        {
                            BranchId        = br.Id,
                            Title           = $"{cat} — {period:MMM yyyy}",
                            Category        = cat,
                            Amount          = Math.Round(amt, 0),
                            ExpenseDate     = new DateTime(period.Year, period.Month, rng.Next(1, 26)),
                            PaymentMethod   = rng.Next(2) == 0 ? "BankTransfer" : "Cash",
                            IsRecurring     = cat is "Rent" or "Utilities" or "Bills",
                            RecurringFrequency = cat is "Rent" or "Utilities" or "Bills" ? "Monthly" : null,
                            ApprovalStatus  = "Approved",
                            CreatedAt       = DateTime.Now.AddMonths(-m),
                        });
                    }
                }
            }
            context.Expenses.AddRange(expenseList);
            await context.SaveChangesAsync();

            // ── Attendance (last 90 days, Mon–Sat, all staff) ─────────────────────────
            var statusPool  = new[] { "Present","Present","Present","Present","Present","Present","Late","Late","Half-Day","Absent" };
            var attList     = new List<Attendance>();

            for (int daysAgo = 90; daysAgo >= 0; daysAgo--)
            {
                var date = DateTime.Now.Date.AddDays(-daysAgo);
                if (date.DayOfWeek is DayOfWeek.Sunday or DayOfWeek.Saturday) continue;

                foreach (var sr in staffRecs)
                {
                    if (date.Date < sr.HireDate.Date) continue;

                    var status  = statusPool[rng.Next(statusPool.Length)];
                    var lateMin = status == "Late" ? rng.Next(15, 90) : 0;
                    var checkIn = status == "Absent" ? (TimeSpan?)null
                                : new TimeSpan(8 + (lateMin > 0 ? 1 : 0), rng.Next(0, 30), 0);
                    var checkOut = status == "Absent" ? (TimeSpan?)null
                                 : status == "Half-Day" ? new TimeSpan(12, rng.Next(0, 30), 0)
                                 : new TimeSpan(17, rng.Next(0, 45), 0);
                    decimal totalHrs = (checkIn == null || checkOut == null) ? 0m
                                     : (decimal)(checkOut.Value - checkIn.Value).TotalHours;
                    decimal ot = totalHrs > 8m ? totalHrs - 8m : 0m;

                    attList.Add(new Attendance
                    {
                        StaffId       = sr.Id,
                        BranchId      = sr.BranchId,
                        Date          = date,
                        Status        = status,
                        CheckInTime   = checkIn,
                        CheckOutTime  = checkOut,
                        TotalHours    = totalHrs,
                        OvertimeHours = ot,
                        LateMinutes   = lateMin,
                        CreatedAt     = date,
                    });
                }

                if (attList.Count >= 200)
                {
                    context.Attendances.AddRange(attList);
                    await context.SaveChangesAsync();
                    attList.Clear();
                }
            }
            if (attList.Any())
            {
                context.Attendances.AddRange(attList);
                await context.SaveChangesAsync();
            }

            // ── Salary Records (last 7 months, all staff) ─────────────────────────────
            var salaryList = new List<SalaryRecord>();
            foreach (var sr in staffRecs)
            {
                decimal baseSal = sr.StaffRoleId == rChef.Id    ? 38000m
                                : sr.StaffRoleId == rBarista.Id  ? 28000m
                                : sr.StaffRoleId == rWaiter.Id   ? 20000m
                                : 22000m; // cashier

                for (int m = 7; m >= 1; m--)
                {
                    var period = DateTime.Now.AddMonths(-m);
                    if (period.Date < sr.HireDate.Date) continue;

                    int workingDays    = 26;
                    int daysPresent    = rng.Next(22, 27);
                    int daysAbsent     = Math.Max(0, workingDays - daysPresent - rng.Next(0, 2));
                    int daysLate       = rng.Next(0, 4);
                    decimal overtime   = rng.Next(0, 15);
                    decimal otRate     = baseSal / 26m / 8m * 1.5m;
                    decimal otPay      = overtime * otRate;
                    decimal attBonus   = daysPresent >= 26 ? 1500m : 0m;
                    decimal bonus      = rng.Next(12) == 0 ? rng.Next(2000, 5001) : 0m;
                    decimal absDed     = daysAbsent * (baseSal / 26m);
                    decimal lateDed    = daysLate * 500m;
                    decimal gross      = baseSal + otPay + attBonus + bonus;
                    decimal deductions = absDed + lateDed;
                    decimal final      = gross - deductions;

                    salaryList.Add(new SalaryRecord
                    {
                        StaffId              = sr.Id,
                        BranchId             = sr.BranchId,
                        Year                 = period.Year,
                        Month                = period.Month,
                        BaseSalary           = baseSal,
                        TotalWorkingDays     = workingDays,
                        DaysPresent          = daysPresent,
                        DaysAbsent           = daysAbsent,
                        DaysLate             = daysLate,
                        DaysHalfDay          = rng.Next(0, 2),
                        AttendancePercentage = Math.Round(daysPresent * 100m / workingDays, 1),
                        OvertimeHours        = overtime,
                        OvertimePay          = Math.Round(otPay, 0),
                        AttendanceBonus      = attBonus,
                        GrossSalary          = Math.Round(gross, 0),
                        AbsenceDeduction     = Math.Round(absDed, 0),
                        LatePenaltyDeduction = lateDed,
                        TotalDeductions      = Math.Round(deductions, 0),
                        BonusAmount          = bonus,
                        FinalSalary          = Math.Round(final, 0),
                        Status               = "Paid",
                        PaymentStatus        = "Paid",
                        PaidDate             = new DateTime(period.Year, period.Month, 28),
                        PaymentMethod        = "Bank Transfer",
                        GeneratedAt          = new DateTime(period.Year, period.Month, 25),
                    });
                }
            }
            context.SalaryRecords.AddRange(salaryList);
            await context.SaveChangesAsync();

            // ── Purchases ─────────────────────────────────────────────────────────────
            var supplierNames = new[] { "Fresh Foods Co.", "Meat Masters Ltd.", "Coffee Central", "Dairy Delights" };
            var purchaseList  = new List<Purchase>();
            for (int m = 6; m >= 0; m--)
            {
                for (int i = 0; i < 4; i++)
                {
                    var inv = invItems[rng.Next(invItems.Length)];
                    int qty = rng.Next(10, 60);
                    purchaseList.Add(new Purchase
                    {
                        SupplierName      = supplierNames[rng.Next(supplierNames.Length)],
                        ItemId            = inv.Id,
                        BranchId          = inv.BranchId,
                        QuantityPurchased = qty,
                        TotalCost         = qty * inv.UnitPrice,
                        DatePurchased     = DateTime.Now.AddMonths(-m).AddDays(rng.Next(1, 26)),
                        Status            = "Received",
                        CreatedAt         = DateTime.Now.AddMonths(-m),
                    });
                }
            }
            context.Purchases.AddRange(purchaseList);
            await context.SaveChangesAsync();

            // ── Feedback ──────────────────────────────────────────────────────────────
            context.Feedbacks.AddRange(
                new Feedback { CustomerId = custUsers[0].Id, BranchId = br1.Id, Rating = 5, Comments = "Best cappuccino in town! Staff are incredibly friendly.",   Category = "Service",      Source = "In-Store", Status = FeedbackStatus.Resolved, Date = DateTime.Now.AddDays(-15), ResolvedAt = DateTime.Now.AddDays(-13) },
                new Feedback { CustomerId = custUsers[1].Id, BranchId = br2.Id, Rating = 4, Comments = "Great food, slightly slow service at peak hours.",          Category = "Food Quality", Source = "Online",   Status = FeedbackStatus.Open,     Date = DateTime.Now.AddDays(-5) },
                new Feedback { CustomerId = custUsers[2].Id, BranchId = br3.Id, Rating = 5, Comments = "Matcha latte is absolutely amazing, will definitely return!", Category = "Product",    Source = "Online",   Status = FeedbackStatus.Resolved, Date = DateTime.Now.AddDays(-20), ResolvedAt = DateTime.Now.AddDays(-18) },
                new Feedback { CustomerId = custUsers[3].Id, BranchId = br4.Id, Rating = 3, Comments = "Avocado toast was good but portion sizes are small.",       Category = "Value",        Source = "In-Store", Status = FeedbackStatus.Open,     Date = DateTime.Now.AddDays(-2) }
            );
            await context.SaveChangesAsync();
        }

        // ── Platform helpers (idempotent) ──

        private static async Task EnsurePlansAsync(ApplicationDbContext context)
        {
            if (await context.Plans.AnyAsync()) return;

            context.Plans.AddRange(
                new Plan
                {
                    Name = "Free", Description = "Core POS for a single branch.",
                    PriceMonthly = 0, MaxBranches = 1, MaxUsers = 5, SortOrder = 0, IsActive = true,
                    Features = string.Join(",", FeatureCatalog.Invoicing)
                },
                new Plan
                {
                    Name = "Starter", Description = "Inventory, suppliers and feedback for a growing shop.",
                    PriceMonthly = 2500, MaxBranches = 3, MaxUsers = 20, SortOrder = 1, IsActive = true,
                    Features = string.Join(",", FeatureCatalog.Invoicing, FeatureCatalog.Inventory,
                        FeatureCatalog.Suppliers, FeatureCatalog.Purchases, FeatureCatalog.Feedback)
                },
                new Plan
                {
                    Name = "Pro", Description = "Everything — analytics, payroll, marketing, all modules.",
                    PriceMonthly = 6000, MaxBranches = 99, MaxUsers = 999, SortOrder = 2, IsActive = true,
                    Features = "*"
                });
            await context.SaveChangesAsync();
        }

        private static async Task EnsurePlatformAdminAsync(ApplicationDbContext context, IAuthService authService)
        {
            // Platform admin lives outside any tenant (TenantId = null). Bypass not needed: the
            // tenant context defaults to "ignore filter" during startup seeding.
            if (await context.Users.AnyAsync(u => u.Role == "PlatformAdmin")) return;

            context.Users.Add(new User
            {
                Name = "Platform Admin",
                Email = "platform@cafe.com",
                Phone = "000-0000",
                Role = "PlatformAdmin",
                TenantId = null,
                PasswordHash = authService.HashPassword("platform123"),
                CreatedDate = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        private static async Task EnsureTenantsHavePlanAsync(ApplicationDbContext context)
        {
            var planless = await context.Tenants.Where(t => t.PlanId == null).ToListAsync();
            if (planless.Count == 0) return;
            var pro = await context.Plans.FirstOrDefaultAsync(p => p.Name == "Pro");
            if (pro == null) return;
            foreach (var t in planless) t.PlanId = pro.Id;
            await context.SaveChangesAsync();
        }
    }
}
