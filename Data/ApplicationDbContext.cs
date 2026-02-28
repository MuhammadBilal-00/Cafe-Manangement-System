using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // User Management
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Staff> Staff { get; set; }
        public DbSet<StaffRole> StaffRoles { get; set; }
        public DbSet<StaffSalary> StaffSalaries { get; set; }
        public DbSet<StaffSchedule> StaffSchedules { get; set; }

        // Branch Management
        public DbSet<Branch> Branches { get; set; }

        // Menu Management
        public DbSet<Category> Categories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemIngredient> MenuItemIngredients { get; set; }
        public DbSet<MenuItemReview> MenuItemReviews { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<DailySpecial> DailySpecials { get; set; }

        // Order Management
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Inventory Management
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryRecipeMapping> InventoryRecipeMappings { get; set; }

        // Feedback & Reports
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<SalesReport> SalesReports { get; set; }

        // Audit
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Todo
        public DbSet<TodoItem> TodoItems { get; set; }

        // Attendance, Salary & Financial
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<SalaryRecord> SalaryRecords { get; set; }
        public DbSet<SalaryAdjustment> SalaryAdjustments { get; set; }
        public DbSet<SalaryPolicy> SalaryPolicies { get; set; }
        public DbSet<Expense> Expenses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureDecimalPrecision(modelBuilder);
            ConfigureUniqueConstraints(modelBuilder);
            ConfigureRelationships(modelBuilder);
            ConfigureDefaultValues(modelBuilder);
            ConfigureCheckConstraints(modelBuilder);
        }

        private void ConfigureDecimalPrecision(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasPrecision(10, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.OriginalPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.CostPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.Quantity)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.ReorderLevel)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryTransaction>()
                .Property(it => it.Quantity)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryTransaction>()
                .Property(it => it.QuantityBefore)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryTransaction>()
                .Property(it => it.QuantityAfter)
                .HasPrecision(10, 2);

            modelBuilder.Entity<InventoryRecipeMapping>()
                .Property(irm => irm.QuantityRequired)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Purchase>()
                .Property(p => p.TotalCost)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.CostPerUnit)
                .HasPrecision(10, 2);

            modelBuilder.Entity<MenuItemIngredient>()
                .Property(mii => mii.ExtraCharge)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.TotalRevenue)
                .HasPrecision(12, 2);

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.AverageOrderValue)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffSalary>()
                .Property(ss => ss.BaseSalary)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffSalary>()
                .Property(ss => ss.HourlyRate)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffSalary>()
                .Property(ss => ss.Bonus)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffSalary>()
                .Property(ss => ss.Deductions)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffRole>()
                .Property(sr => sr.DefaultHourlyRate)
                .HasPrecision(10, 2);

            modelBuilder.Entity<StaffRole>()
                .Property(sr => sr.DefaultMonthlySalary)
                .HasPrecision(10, 2);

            modelBuilder.Entity<DailySpecial>()
                .Property(ds => ds.SpecialPrice)
                .HasPrecision(10, 2);

            // SalaryRecord decimal precision
            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.BaseSalary)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.BonusAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.DeductionAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.FinalSalary)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.AttendancePercentage)
                .HasPrecision(5, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.OvertimeHours)
                .HasPrecision(5, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.OvertimePay)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.AttendanceBonus)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.AbsenceDeduction)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.HalfDayDeduction)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.LatePenaltyDeduction)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.GrossSalary)
                .HasPrecision(10, 2);

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.TotalDeductions)
                .HasPrecision(10, 2);

            // Attendance decimal precision
            modelBuilder.Entity<Attendance>()
                .Property(a => a.TotalHours)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Attendance>()
                .Property(a => a.OvertimeHours)
                .HasPrecision(5, 2);

            // SalaryAdjustment decimal precision
            modelBuilder.Entity<SalaryAdjustment>()
                .Property(sa => sa.Amount)
                .HasPrecision(10, 2);

            // SalaryPolicy decimal precision
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.AbsenceDeductionFactor).HasPrecision(5, 2);
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.HalfDayDeductionFactor).HasPrecision(5, 2);
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.LatePenaltyFactor).HasPrecision(5, 2);
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.OvertimeMultiplier).HasPrecision(5, 2);
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.AttendanceBonusPercentage).HasPrecision(5, 2);
            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.StandardDailyHours).HasPrecision(5, 2);

            // Expense decimal precision
            modelBuilder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasPrecision(10, 2);
        }

        private void ConfigureUniqueConstraints(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Staff>()
                .HasIndex(s => s.EmployeeId)
                .IsUnique()
                .HasFilter("[EmployeeId] IS NOT NULL");

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => i.Name)
                .IsUnique();

            // Attendance: one record per staff per date
            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.StaffId, a.Date })
                .IsUnique();

            // SalaryRecord: one record per staff per month
            modelBuilder.Entity<SalaryRecord>()
                .HasIndex(sr => new { sr.StaffId, sr.Year, sr.Month })
                .IsUnique();

            // SalaryRecord: composite index for dashboard queries
            modelBuilder.Entity<SalaryRecord>()
                .HasIndex(sr => new { sr.BranchId, sr.Year, sr.Month });
        }

        private void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // User relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.ManagedBranches)
                .WithOne(b => b.Manager)
                .HasForeignKey(b => b.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasMany(u => u.StaffRecords)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Orders)
                .WithOne(o => o.Customer)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Feedbacks)
                .WithOne(f => f.Customer)
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Branch relationships
            modelBuilder.Entity<Branch>()
                .HasMany(b => b.MenuItems)
                .WithOne(m => m.Branch)
                .HasForeignKey(m => m.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Branch>()
                .HasMany(b => b.Orders)
                .WithOne(o => o.Branch)
                .HasForeignKey(o => o.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Branch>()
                .HasMany(b => b.InventoryItems)
                .WithOne(i => i.Branch)
                .HasForeignKey(i => i.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Branch>()
                .HasMany(b => b.Staff)
                .WithOne(s => s.Branch)
                .HasForeignKey(s => s.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Branch>()
                .HasMany(b => b.Feedbacks)
                .WithOne(f => f.Branch)
                .HasForeignKey(f => f.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // MenuItem relationships
            modelBuilder.Entity<MenuItem>()
                .HasOne(m => m.Category)
                .WithMany(c => c.MenuItems)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MenuItem>()
                .HasMany(m => m.OrderItems)
                .WithOne(oi => oi.MenuItem)
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MenuItem>()
                .HasMany(m => m.Ingredients)
                .WithOne(mii => mii.MenuItem)
                .HasForeignKey(mii => mii.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MenuItem>()
                .HasMany(m => m.Reviews)
                .WithOne(mir => mir.MenuItem)
                .HasForeignKey(mir => mir.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Order relationships
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Staff relationships
            modelBuilder.Entity<Staff>()
                .HasOne(s => s.StaffRole)
                .WithMany(sr => sr.StaffMembers)
                .HasForeignKey(s => s.StaffRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Staff>()
                .HasMany(s => s.SalaryHistory)
                .WithOne(ss => ss.Staff)
                .HasForeignKey(ss => ss.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Staff>()
                .HasMany(s => s.Schedules)
                .WithOne(ss => ss.Staff)
                .HasForeignKey(ss => ss.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            // Inventory relationships
            modelBuilder.Entity<InventoryItem>()
                .HasMany(i => i.Purchases)
                .WithOne(p => p.Item)
                .HasForeignKey(p => p.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            

            // Many-to-many: MenuItemIngredients
            modelBuilder.Entity<MenuItemIngredient>()
                .HasKey(mii => new { mii.MenuItemId, mii.IngredientId });

            modelBuilder.Entity<MenuItemIngredient>()
                .HasOne(mii => mii.MenuItem)
                .WithMany(m => m.Ingredients)
                .HasForeignKey(mii => mii.MenuItemId);

            modelBuilder.Entity<MenuItemIngredient>()
                .HasOne(mii => mii.Ingredient)
                .WithMany(i => i.MenuItemIngredients)
                .HasForeignKey(mii => mii.IngredientId);

            // DailySpecial relationships
            modelBuilder.Entity<DailySpecial>()
                .HasOne(ds => ds.MenuItem)
                .WithMany()
                .HasForeignKey(ds => ds.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailySpecial>()
                .HasOne(ds => ds.Branch)
                .WithMany()
                .HasForeignKey(ds => ds.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Attendance relationships
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Staff)
                .WithMany()
                .HasForeignKey(a => a.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Branch)
                .WithMany()
                .HasForeignKey(a => a.BranchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.MarkedBy)
                .WithMany()
                .HasForeignKey(a => a.MarkedById)
                .OnDelete(DeleteBehavior.NoAction);

            // SalaryRecord relationships
            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.Staff)
                .WithMany()
                .HasForeignKey(sr => sr.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.Branch)
                .WithMany()
                .HasForeignKey(sr => sr.BranchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.GeneratedBy)
                .WithMany()
                .HasForeignKey(sr => sr.GeneratedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.PolicyUsed)
                .WithMany()
                .HasForeignKey(sr => sr.PolicyIdUsed)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.FinalizedBy)
                .WithMany()
                .HasForeignKey(sr => sr.FinalizedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalaryRecord>()
                .HasOne(sr => sr.UnlockedBy)
                .WithMany()
                .HasForeignKey(sr => sr.UnlockedById)
                .OnDelete(DeleteBehavior.NoAction);

            // SalaryAdjustment relationships
            modelBuilder.Entity<SalaryAdjustment>()
                .HasOne(sa => sa.SalaryRecord)
                .WithMany(sr => sr.Adjustments)
                .HasForeignKey(sa => sa.SalaryRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalaryAdjustment>()
                .HasOne(sa => sa.CreatedBy)
                .WithMany()
                .HasForeignKey(sa => sa.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Expense relationships
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);
        }

        private void ConfigureDefaultValues(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Branch>()
                .Property(b => b.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Branch>()
                .Property(b => b.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Category>()
                .Property(c => c.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Category>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Availability)
                .HasDefaultValue(true);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.IsVegetarian)
                .HasDefaultValue(false);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.IsVegan)
                .HasDefaultValue(false);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.IsGlutenFree)
                .HasDefaultValue(false);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.PreparationTime)
                .HasDefaultValue(15);

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.Quantity)
                .HasDefaultValue(0);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.ReorderLevel)
                .HasDefaultValue(0);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.LastUpdated)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.UnitPrice)
                .HasDefaultValue(0);

            

            modelBuilder.Entity<InventoryTransaction>()
                .Property(it => it.TransactionDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Purchase>()
                .Property(p => p.DatePurchased)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Staff>()
                .Property(s => s.HireDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Staff>()
                .Property(s => s.EmploymentStatus)
                .HasDefaultValue("Active");

            modelBuilder.Entity<Staff>()
                .Property(s => s.EmploymentType)
                .HasDefaultValue("Full-time");

            modelBuilder.Entity<Staff>()
                .Property(s => s.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Customer>()
                .Property(c => c.LoyaltyPoints)
                .HasDefaultValue(0);

            modelBuilder.Entity<Customer>()
                .Property(c => c.JoinDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Customer>()
                .Property(c => c.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Feedback>()
                .Property(f => f.Date)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Feedback>()
                .Property(f => f.IsResolved)
                .HasDefaultValue(false);

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.ReportDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.TotalRevenue)
                .HasDefaultValue(0);

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.TotalOrders)
                .HasDefaultValue(0);

            modelBuilder.Entity<SalesReport>()
                .Property(sr => sr.AverageOrderValue)
                .HasDefaultValue(0);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.IsAllergen)
                .HasDefaultValue(false);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.Unit)
                .HasDefaultValue("g");

            modelBuilder.Entity<DailySpecial>()
                .Property(ds => ds.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<MenuItemReview>()
                .Property(mir => mir.ReviewDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<MenuItemReview>()
                .Property(mir => mir.IsVerified)
                .HasDefaultValue(false);

            modelBuilder.Entity<MenuItemIngredient>()
                .Property(mii => mii.Unit)
                .HasDefaultValue("g");

            modelBuilder.Entity<MenuItemIngredient>()
                .Property(mii => mii.IsOptional)
                .HasDefaultValue(false);

            // Attendance defaults
            modelBuilder.Entity<Attendance>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Attendance>()
                .Property(a => a.Status)
                .HasDefaultValue("Present");

            // SalaryRecord defaults
            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.GeneratedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.PaymentStatus)
                .HasDefaultValue("Pending");

            modelBuilder.Entity<SalaryRecord>()
                .Property(sr => sr.Status)
                .HasDefaultValue("Draft");

            // SalaryAdjustment defaults
            modelBuilder.Entity<SalaryAdjustment>()
                .Property(sa => sa.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // Expense defaults
            modelBuilder.Entity<Expense>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Expense>()
                .Property(e => e.ApprovalStatus)
                .HasDefaultValue("Approved");

            modelBuilder.Entity<Expense>()
                .Property(e => e.IsRecurring)
                .HasDefaultValue(false);
        }

        private void ConfigureCheckConstraints(ModelBuilder modelBuilder)
        {
            // ── NEW SYNTAX: ToTable(t => t.HasCheckConstraint(...)) ──

            modelBuilder.Entity<Feedback>()
                .ToTable(t => t.HasCheckConstraint("CK_Feedback_Rating",
                    "[Rating] >= 1 AND [Rating] <= 5"));

            modelBuilder.Entity<MenuItemReview>()
                .ToTable(t => t.HasCheckConstraint("CK_MenuItemReview_Rating",
                    "[Rating] >= 1 AND [Rating] <= 5"));

            modelBuilder.Entity<MenuItem>()
                .ToTable(t => t.HasCheckConstraint("CK_MenuItem_Price",
                    "[Price] > 0"));

            modelBuilder.Entity<MenuItem>()
                .ToTable(t => t.HasCheckConstraint("CK_MenuItem_SpiceLevel",
                    "[SpiceLevel] >= 0 AND [SpiceLevel] <= 5"));

            modelBuilder.Entity<Order>()
                .ToTable(t => t.HasCheckConstraint("CK_Order_TotalAmount",
                    "[TotalAmount] > 0"));

            modelBuilder.Entity<OrderItem>()
                .ToTable(t => t.HasCheckConstraint("CK_OrderItem_Quantity",
                    "[Quantity] > 0"));

            modelBuilder.Entity<OrderItem>()
                .ToTable(t => t.HasCheckConstraint("CK_OrderItem_Price",
                    "[Price] > 0"));

            modelBuilder.Entity<Staff>()
                .ToTable(t => t.HasCheckConstraint("CK_Staff_PerformanceRating",
                    "[PerformanceRating] >= 1 AND [PerformanceRating] <= 5"));

            modelBuilder.Entity<InventoryItem>()
                .ToTable(t => t.HasCheckConstraint("CK_InventoryItem_Quantity",
                    "[Quantity] >= 0"));

            modelBuilder.Entity<InventoryItem>()
                .ToTable(t => t.HasCheckConstraint("CK_InventoryItem_ReorderLevel",
                    "[ReorderLevel] >= 0"));

            modelBuilder.Entity<Purchase>()
                .ToTable(t => t.HasCheckConstraint("CK_Purchase_QuantityPurchased",
                    "[QuantityPurchased] > 0"));

            modelBuilder.Entity<Purchase>()
                .ToTable(t => t.HasCheckConstraint("CK_Purchase_TotalCost",
                    "[TotalCost] > 0"));

            modelBuilder.Entity<Ingredient>()
                .ToTable(t => t.HasCheckConstraint("CK_Ingredient_CostPerUnit",
                    "[CostPerUnit] >= 0"));

            modelBuilder.Entity<DailySpecial>()
                .ToTable(t => t.HasCheckConstraint("CK_DailySpecial_SpecialPrice",
                    "[SpecialPrice] >= 0"));

            // Audit Log defaults
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Timestamp)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.Timestamp);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.EntityType);

            // Attendance check constraint
            modelBuilder.Entity<Attendance>()
                .ToTable(t => t.HasCheckConstraint("CK_Attendance_Status",
                    "[Status] IN ('Present','Absent','Late','Half-Day')"));

            // SalaryRecord check constraints
            modelBuilder.Entity<SalaryRecord>()
                .ToTable(t => t.HasCheckConstraint("CK_SalaryRecord_PaymentStatus",
                    "[PaymentStatus] IN ('Pending','Paid','Cancelled')"));

            modelBuilder.Entity<SalaryRecord>()
                .ToTable(t => t.HasCheckConstraint("CK_SalaryRecord_Month",
                    "[Month] >= 1 AND [Month] <= 12"));

            modelBuilder.Entity<SalaryRecord>()
                .ToTable(t => t.HasCheckConstraint("CK_SalaryRecord_Status",
                    "[Status] IN ('Draft','Finalized','Paid')"));

            // SalaryAdjustment check constraints
            modelBuilder.Entity<SalaryAdjustment>()
                .ToTable(t => t.HasCheckConstraint("CK_SalaryAdjustment_Type",
                    "[Type] IN ('Bonus','Deduction')"));

            modelBuilder.Entity<SalaryAdjustment>()
                .ToTable(t => t.HasCheckConstraint("CK_SalaryAdjustment_Amount",
                    "[Amount] > 0"));

            // SalaryPolicy relationships & defaults
            modelBuilder.Entity<SalaryPolicy>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalaryPolicy>()
                .HasOne(p => p.UpdatedBy)
                .WithMany()
                .HasForeignKey(p => p.UpdatedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<SalaryPolicy>()
                .Property(p => p.IsActive)
                .HasDefaultValue(false);

            // Expense check constraints
            modelBuilder.Entity<Expense>()
                .ToTable(t => t.HasCheckConstraint("CK_Expense_Amount",
                    "[Amount] > 0"));

            modelBuilder.Entity<Expense>()
                .ToTable(t => t.HasCheckConstraint("CK_Expense_ApprovalStatus",
                    "[ApprovalStatus] IN ('Pending','Approved','Rejected')"));
        }
    }
}