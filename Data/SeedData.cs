using System;
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
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            // Only seed if database has no users
            if (await context.Users.AnyAsync())
                return;

            // --- Users ---
            var owner = new User { Name = "Admin Owner", Email = "admin@cafe.com", Phone = "555-0100", Role = "Owner", PasswordHash = authService.HashPassword("admin123"), CreatedDate = DateTime.Now.AddMonths(-6) };
            var manager1 = new User { Name = "Sarah Johnson", Email = "sarah@cafe.com", Phone = "555-0101", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-5) };
            var manager2 = new User { Name = "Michael Chen", Email = "michael@cafe.com", Phone = "555-0102", Role = "BranchManager", PasswordHash = authService.HashPassword("manager123"), CreatedDate = DateTime.Now.AddMonths(-4) };
            var staff1 = new User { Name = "Emily Davis", Email = "emily@cafe.com", Phone = "555-0201", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-3) };
            var staff2 = new User { Name = "James Wilson", Email = "james@cafe.com", Phone = "555-0202", Role = "Staff", PasswordHash = authService.HashPassword("staff123"), CreatedDate = DateTime.Now.AddMonths(-3) };
            var customer1 = new User { Name = "Alice Brown", Email = "alice@example.com", Phone = "555-0301", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-2) };
            var customer2 = new User { Name = "Bob Martinez", Email = "bob@example.com", Phone = "555-0302", Role = "Customer", PasswordHash = authService.HashPassword("cust123"), CreatedDate = DateTime.Now.AddMonths(-1) };

            context.Users.AddRange(owner, manager1, manager2, staff1, staff2, customer1, customer2);
            await context.SaveChangesAsync();

            // --- Branches ---
            var branch1 = new Branch { Name = "Downtown Cafe", Location = "123 Main Street", ContactInfo = "555-1001", OpeningHours = "7:00 AM - 10:00 PM", ManagerId = manager1.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-5) };
            var branch2 = new Branch { Name = "Uptown Bistro", Location = "456 Oak Avenue", ContactInfo = "555-1002", OpeningHours = "8:00 AM - 9:00 PM", ManagerId = manager2.Id, IsActive = true, CreatedDate = DateTime.Now.AddMonths(-4) };

            context.Branches.AddRange(branch1, branch2);
            await context.SaveChangesAsync();

            // --- Staff Roles ---
            var barista = new StaffRole { RoleName = "Barista", Description = "Prepares coffee and beverages", DefaultHourlyRate = 15.00m, DefaultMonthlySalary = 2400m, IsActive = true, IsSystemRole = false };
            var chef = new StaffRole { RoleName = "Chef", Description = "Prepares food items", DefaultHourlyRate = 20.00m, DefaultMonthlySalary = 3200m, IsActive = true, IsSystemRole = false };
            var cashier = new StaffRole { RoleName = "Cashier", Description = "Handles transactions", DefaultHourlyRate = 13.00m, DefaultMonthlySalary = 2080m, IsActive = true, IsSystemRole = false };

            context.StaffRoles.AddRange(barista, chef, cashier);
            await context.SaveChangesAsync();

            // --- Staff Records ---
            var staffRec1 = new Staff { UserId = staff1.Id, StaffRoleId = barista.Id, BranchId = branch1.Id, HireDate = DateTime.Now.AddMonths(-3), EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Beverages", EmployeeId = "EMP001", PerformanceRating = 4, IsActive = true };
            var staffRec2 = new Staff { UserId = staff2.Id, StaffRoleId = chef.Id, BranchId = branch2.Id, HireDate = DateTime.Now.AddMonths(-3), EmploymentStatus = "Active", EmploymentType = "Full-time", Department = "Kitchen", EmployeeId = "EMP002", PerformanceRating = 5, IsActive = true };

            context.Staff.AddRange(staffRec1, staffRec2);
            await context.SaveChangesAsync();

            // --- Customers ---
            var cust1 = new Customer { UserId = customer1.Id, LoyaltyPoints = 150, JoinDate = DateTime.Now.AddMonths(-2), IsActive = true };
            var cust2 = new Customer { UserId = customer2.Id, LoyaltyPoints = 75, JoinDate = DateTime.Now.AddMonths(-1), IsActive = true };

            context.Customers.AddRange(cust1, cust2);
            await context.SaveChangesAsync();

            // --- Categories ---
            var catCoffee = new Category { Name = "Coffee", Description = "Hot and cold coffee beverages", IsActive = true, CreatedDate = DateTime.Now.AddMonths(-5) };
            var catFood = new Category { Name = "Food", Description = "Sandwiches, pastries and meals", IsActive = true, CreatedDate = DateTime.Now.AddMonths(-5) };
            var catDessert = new Category { Name = "Desserts", Description = "Sweet treats and desserts", IsActive = true, CreatedDate = DateTime.Now.AddMonths(-5) };

            context.Categories.AddRange(catCoffee, catFood, catDessert);
            await context.SaveChangesAsync();

            // --- Menu Items ---
            var items = new[]
            {
                new MenuItem { Name = "Espresso", Description = "Rich and bold espresso shot", Price = 3.50m, OriginalPrice = 3.50m, CostPrice = 0.80m, CategoryId = catCoffee.Id, BranchId = branch1.Id, Availability = true, PreparationTime = 5 },
                new MenuItem { Name = "Cappuccino", Description = "Espresso with steamed milk foam", Price = 4.50m, OriginalPrice = 4.50m, CostPrice = 1.20m, CategoryId = catCoffee.Id, BranchId = branch1.Id, Availability = true, PreparationTime = 7 },
                new MenuItem { Name = "Latte", Description = "Smooth espresso with steamed milk", Price = 4.75m, OriginalPrice = 4.75m, CostPrice = 1.30m, CategoryId = catCoffee.Id, BranchId = branch2.Id, Availability = true, PreparationTime = 7 },
                new MenuItem { Name = "Club Sandwich", Description = "Triple-decker with chicken and bacon", Price = 8.99m, OriginalPrice = 8.99m, CostPrice = 3.50m, CategoryId = catFood.Id, BranchId = branch1.Id, Availability = true, PreparationTime = 15 },
                new MenuItem { Name = "Caesar Salad", Description = "Fresh romaine with parmesan", Price = 7.50m, OriginalPrice = 7.50m, CostPrice = 2.80m, CategoryId = catFood.Id, BranchId = branch2.Id, Availability = true, PreparationTime = 10 },
                new MenuItem { Name = "Chocolate Cake", Description = "Rich dark chocolate layer cake", Price = 5.99m, OriginalPrice = 5.99m, CostPrice = 2.00m, CategoryId = catDessert.Id, BranchId = branch1.Id, Availability = true, PreparationTime = 5 },
            };

            context.MenuItems.AddRange(items);
            await context.SaveChangesAsync();

            // --- Inventory Items ---
            var inv = new[]
            {
                new InventoryItem { Name = "Coffee Beans", Quantity = 50, Unit = "kg", BranchId = branch1.Id, UnitPrice = 12.00m, ReorderLevel = 10, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Milk", Quantity = 30, Unit = "L", BranchId = branch1.Id, UnitPrice = 2.50m, ReorderLevel = 15, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Sugar", Quantity = 5, Unit = "kg", BranchId = branch1.Id, UnitPrice = 1.80m, ReorderLevel = 8, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Coffee Beans", Quantity = 40, Unit = "kg", BranchId = branch2.Id, UnitPrice = 12.00m, ReorderLevel = 10, LastUpdated = DateTime.Now },
                new InventoryItem { Name = "Chicken Breast", Quantity = 3, Unit = "kg", BranchId = branch2.Id, UnitPrice = 8.00m, ReorderLevel = 5, LastUpdated = DateTime.Now },
            };

            context.InventoryItems.AddRange(inv);
            await context.SaveChangesAsync();

            // --- Orders (spread over last 3 months for chart data) ---
            var random = new Random(42);
            var orderNum = 1001;
            for (int daysAgo = 90; daysAgo >= 0; daysAgo -= 3)
            {
                var date = DateTime.Now.AddDays(-daysAgo);
                var customerId = random.Next(2) == 0 ? customer1.Id : customer2.Id;
                var branchId = random.Next(2) == 0 ? branch1.Id : branch2.Id;
                var menuItem = items[random.Next(items.Length)];
                var qty = random.Next(1, 4);
                var total = menuItem.Price * qty;
                var status = daysAgo > 2 ? "Completed" : (random.Next(3) == 0 ? "Pending" : "Completed");

                var order = new Order
                {
                    OrderNumber = $"ORD-{orderNum++}",
                    CustomerId = customerId,
                    BranchId = branchId,
                    OrderDate = date,
                    Status = status,
                    TotalAmount = total
                };

                context.Orders.Add(order);
                await context.SaveChangesAsync();

                context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = menuItem.Id,
                    Quantity = qty,
                    Price = menuItem.Price
                });
            }

            await context.SaveChangesAsync();

            // --- Feedback ---
            context.Feedbacks.AddRange(
                new Feedback { CustomerId = customer1.Id, BranchId = branch1.Id, Rating = 5, Comments = "Excellent coffee and service!", Category = "Service", Source = "In-Store", Status = FeedbackStatus.Resolved, Date = DateTime.Now.AddDays(-10), ResolvedAt = DateTime.Now.AddDays(-8) },
                new Feedback { CustomerId = customer2.Id, BranchId = branch2.Id, Rating = 4, Comments = "Great food, slightly slow service", Category = "Food Quality", Source = "Online", Status = FeedbackStatus.Open, Date = DateTime.Now.AddDays(-3) }
            );

            await context.SaveChangesAsync();
        }
    }
}
