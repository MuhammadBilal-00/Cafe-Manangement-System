using Cafe.Models;
using Cafe.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Cafe.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ITenantContext? _tenantContext;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext? tenantContext = null)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        // ── Multi-tenancy: live values the global query filters read per query. EF parameterizes
        //    these (they are members on the context instance), so the model is cached once but the
        //    tenant value is re-evaluated on every query. ──
        private bool IgnoreTenantFilterFlag => _tenantContext?.IgnoreTenantFilter ?? true;
        private int CurrentTenantIdValue => _tenantContext?.CurrentTenantId ?? 0;

        // ── SaaS platform ──
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

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

        // Phase 1: POS & restaurant core
        public DbSet<RestaurantTable> RestaurantTables { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // Phase 2: menu & product depth
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ModifierGroup> ModifierGroups { get; set; }
        public DbSet<Modifier> Modifiers { get; set; }
        public DbSet<MenuItemModifierGroup> MenuItemModifierGroups { get; set; }
        public DbSet<Combo> Combos { get; set; }
        public DbSet<ComboItem> ComboItems { get; set; }
        public DbSet<PriceGroup> PriceGroups { get; set; }
        public DbSet<MenuItemPrice> MenuItemPrices { get; set; }

        // Phase 3: inventory & supply chain
        public DbSet<StockTransfer> StockTransfers { get; set; }
        public DbSet<StockTransferItem> StockTransferItems { get; set; }
        public DbSet<StockAdjustment> StockAdjustments { get; set; }
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }
        public DbSet<ProductionOrder> ProductionOrders { get; set; }
        public DbSet<ProductionInput> ProductionInputs { get; set; }

        // Phase 4: sales lifecycle, returns, receivables
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<SellReturn> SellReturns { get; set; }
        public DbSet<SellReturnLine> SellReturnLines { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnLine> PurchaseReturnLines { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<TaxGroup> TaxGroups { get; set; }
        public DbSet<Tax> Taxes { get; set; }

        // Phase 5: accounting
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalLine> JournalLines { get; set; }
        public DbSet<PaymentAccount> PaymentAccounts { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<BudgetLine> BudgetLines { get; set; }

        // Phase 6: customer portal & marketing
        public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
        public DbSet<GiftCard> GiftCards { get; set; }
        public DbSet<GiftCardTransaction> GiftCardTransactions { get; set; }
        public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<FollowUp> FollowUps { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<SmsQueue> SmsQueues { get; set; }

        // Phase 7: HR depth
        public DbSet<Department> Departments { get; set; }
        public DbSet<Designation> Designations { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<SalesTarget> SalesTargets { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }

        // Phase 8: productivity / essentials
        public DbSet<Document> Documents { get; set; }
        public DbSet<Memo> Memos { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; set; }

        // Phase 9: logistics + POS profiles
        public DbSet<Rider> Riders { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<PosProfile> PosProfiles { get; set; }

        // Inventory Management
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryRecipeMapping> InventoryRecipeMappings { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

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

        // Notifications & Email
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EmailQueue> EmailQueues { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }

        // Checkout: Promos, Bank Partnerships, Invoicing, Branch settings
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<Partnership> Partnerships { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<BranchSetting> BranchSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureDecimalPrecision(modelBuilder);
            ConfigureUniqueConstraints(modelBuilder);
            ConfigureRelationships(modelBuilder);
            ConfigureDefaultValues(modelBuilder);
            ConfigureCheckConstraints(modelBuilder);
            ConfigureNotifications(modelBuilder);
            ConfigureCheckoutModules(modelBuilder);
            ConfigurePhase1Pos(modelBuilder);
            ConfigurePhase2Menu(modelBuilder);
            ConfigurePhase3Inventory(modelBuilder);
            ConfigurePhase4Sales(modelBuilder);
            ConfigurePhase5Accounting(modelBuilder);
            ConfigurePhase6Marketing(modelBuilder);
            ConfigurePhase7Hr(modelBuilder);
            ConfigurePhase8Essentials(modelBuilder);
            ConfigurePhase9Logistics(modelBuilder);
            ConfigureMultiTenancy(modelBuilder);
        }

        /// <summary>Phase 9 (platform/system): delivery/rider, shipments, POS profiles.</summary>
        private void ConfigurePhase9Logistics(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rider>().HasOne<Branch>().WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Delivery>().HasOne(d => d.Order).WithMany().HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Delivery>().HasOne(d => d.Rider).WithMany().HasForeignKey(d => d.RiderId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Delivery>().ToTable(t => t.HasCheckConstraint("CK_Delivery_Status", "[Status] IN ('Pending','Assigned','PickedUp','Delivered','Failed')"));
            modelBuilder.Entity<Delivery>().HasIndex(d => d.OrderId);
            modelBuilder.Entity<Shipment>().ToTable(t => t.HasCheckConstraint("CK_Shipment_Status", "[Status] IN ('Preparing','Shipped','InTransit','Delivered','Returned')"));
        }

        /// <summary>Phase 8 (productivity/essentials): documents, memos, reminders, messages, KB.</summary>
        private void ConfigurePhase8Essentials(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Message>().HasOne(m => m.FromUser).WithMany().HasForeignKey(m => m.FromUserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Message>().HasOne(m => m.ToUser).WithMany().HasForeignKey(m => m.ToUserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Message>().HasIndex(m => new { m.TenantId, m.ToUserId, m.IsRead });
            modelBuilder.Entity<Reminder>().HasIndex(r => new { r.TenantId, r.OwnerId, r.Done });
        }

        /// <summary>Phase 7 (HR depth): leave, holidays, departments/designations, targets, documents.</summary>
        private void ConfigurePhase7Hr(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Department>().HasIndex(d => new { d.TenantId, d.Name }).IsUnique();
            modelBuilder.Entity<Designation>().HasIndex(d => new { d.TenantId, d.Name }).IsUnique();
            modelBuilder.Entity<LeaveType>().HasIndex(l => new { l.TenantId, l.Name }).IsUnique();

            modelBuilder.Entity<Staff>().HasOne(s => s.DepartmentRef).WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Staff>().HasOne(s => s.DesignationRef).WithMany().HasForeignKey(s => s.DesignationId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<LeaveRequest>().HasOne(l => l.Staff).WithMany().HasForeignKey(l => l.StaffId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LeaveRequest>().HasOne(l => l.LeaveType).WithMany().HasForeignKey(l => l.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LeaveRequest>().ToTable(t => t.HasCheckConstraint("CK_LeaveRequest_Status", "[Status] IN ('Pending','Approved','Rejected')"));

            modelBuilder.Entity<Holiday>().HasOne<Branch>().WithMany().HasForeignKey(h => h.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SalesTarget>().HasOne(s => s.Staff).WithMany().HasForeignKey(s => s.StaffId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EmployeeDocument>().HasOne(e => e.Staff).WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.NoAction);
        }

        /// <summary>Phase 6 (customer portal &amp; marketing): loyalty, gift cards, templates, CRM, SMS.</summary>
        private void ConfigurePhase6Marketing(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LoyaltyTransaction>().HasOne(l => l.Customer).WithMany().HasForeignKey(l => l.CustomerUserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LoyaltyTransaction>().HasIndex(l => new { l.TenantId, l.CustomerUserId });
            modelBuilder.Entity<LoyaltyTransaction>().ToTable(t => t.HasCheckConstraint("CK_Loyalty_Type", "[Type] IN ('Earn','Redeem','Adjust')"));

            modelBuilder.Entity<GiftCard>().HasIndex(g => new { g.TenantId, g.Code }).IsUnique();
            modelBuilder.Entity<GiftCardTransaction>().HasOne(t => t.GiftCard).WithMany(g => g.Transactions).HasForeignKey(t => t.GiftCardId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NotificationTemplate>().HasIndex(t => new { t.TenantId, t.Key }).IsUnique();
            modelBuilder.Entity<NotificationTemplate>().ToTable(t => t.HasCheckConstraint("CK_Template_Channel", "[Channel] IN ('Email','SMS','InApp')"));

            modelBuilder.Entity<Lead>().ToTable(t => t.HasCheckConstraint("CK_Lead_Status", "[Status] IN ('New','Contacted','Qualified','Won','Lost')"));
            modelBuilder.Entity<FollowUp>().HasOne(f => f.Lead).WithMany(l => l.FollowUps).HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Campaign>().ToTable(t => t.HasCheckConstraint("CK_Campaign_Channel", "[Channel] IN ('Email','SMS')"));
            modelBuilder.Entity<SmsQueue>().HasIndex(s => s.IsSent);
        }

        /// <summary>Phase 5 (accounting): chart of accounts, journals, payment accounts, budgets.</summary>
        private void ConfigurePhase5Accounting(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>().HasIndex(a => new { a.TenantId, a.Code }).IsUnique();
            modelBuilder.Entity<Account>().HasOne(a => a.Parent).WithMany().HasForeignKey(a => a.ParentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Account>().ToTable(t => t.HasCheckConstraint("CK_Account_Type", "[Type] IN ('Asset','Liability','Equity','Income','Expense')"));

            modelBuilder.Entity<JournalEntry>().HasOne<Branch>().WithMany().HasForeignKey(j => j.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<JournalEntry>().ToTable(t => t.HasCheckConstraint("CK_JournalEntry_Status", "[Status] IN ('Draft','Posted','Void')"));
            // Idempotency for auto-posting: one entry per (source doc).
            modelBuilder.Entity<JournalEntry>().HasIndex(j => new { j.TenantId, j.SourceType, j.SourceId })
                .IsUnique().HasFilter("[SourceId] IS NOT NULL");

            modelBuilder.Entity<JournalLine>().HasOne(l => l.JournalEntry).WithMany(j => j.Lines).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<JournalLine>().HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<JournalLine>().HasIndex(l => new { l.TenantId, l.AccountId }); // report aggregates

            modelBuilder.Entity<PaymentAccount>().HasOne(p => p.Account).WithMany().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BudgetLine>().HasOne(l => l.Budget).WithMany(b => b.Lines).HasForeignKey(l => l.BudgetId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<BudgetLine>().HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.NoAction);
        }

        /// <summary>Phase 4 (sales lifecycle, returns, receivables): quotations, returns, supplier
        /// payments, tax groups. NoAction/SetNull into hubs; child lines cascade from their parent.</summary>
        private void ConfigurePhase4Sales(ModelBuilder modelBuilder)
        {
            // Quotations
            modelBuilder.Entity<Quotation>().HasIndex(q => new { q.TenantId, q.QuotationNumber }).IsUnique();
            modelBuilder.Entity<Quotation>().HasOne(q => q.Branch).WithMany().HasForeignKey(q => q.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Quotation>().HasOne(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Quotation>().ToTable(t => t.HasCheckConstraint("CK_Quotation_Status", "[Status] IN ('Draft','Sent','Accepted','Converted','Expired','Cancelled')"));
            modelBuilder.Entity<QuotationItem>().HasOne(i => i.Quotation).WithMany(q => q.Items).HasForeignKey(i => i.QuotationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<QuotationItem>().HasOne(i => i.MenuItem).WithMany().HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.NoAction);

            // Sell returns
            modelBuilder.Entity<SellReturn>().HasIndex(r => new { r.TenantId, r.ReturnNumber }).IsUnique();
            modelBuilder.Entity<SellReturn>().HasOne(r => r.Branch).WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SellReturn>().HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SellReturn>().ToTable(t => t.HasCheckConstraint("CK_SellReturn_Status", "[Status] IN ('Pending','Approved','Rejected')"));
            modelBuilder.Entity<SellReturnLine>().HasOne(l => l.SellReturn).WithMany(r => r.Lines).HasForeignKey(l => l.SellReturnId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SellReturnLine>().HasOne(l => l.InventoryItem).WithMany().HasForeignKey(l => l.InventoryItemId).OnDelete(DeleteBehavior.NoAction);

            // Purchase returns
            modelBuilder.Entity<PurchaseReturn>().HasIndex(r => new { r.TenantId, r.ReturnNumber }).IsUnique();
            modelBuilder.Entity<PurchaseReturn>().HasOne(r => r.Branch).WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PurchaseReturn>().HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<PurchaseReturn>().ToTable(t => t.HasCheckConstraint("CK_PurchaseReturn_Status", "[Status] IN ('Pending','Approved','Rejected')"));
            modelBuilder.Entity<PurchaseReturnLine>().HasOne(l => l.PurchaseReturn).WithMany(r => r.Lines).HasForeignKey(l => l.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PurchaseReturnLine>().HasOne(l => l.InventoryItem).WithMany().HasForeignKey(l => l.InventoryItemId).OnDelete(DeleteBehavior.NoAction);

            // Supplier payments (AP)
            modelBuilder.Entity<SupplierPayment>().HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SupplierPayment>().HasIndex(p => p.SupplierId);

            // Tax groups
            modelBuilder.Entity<TaxGroup>().HasIndex(g => new { g.TenantId, g.Name }).IsUnique();
            modelBuilder.Entity<Tax>().HasOne(t => t.TaxGroup).WithMany(g => g.Taxes).HasForeignKey(t => t.TaxGroupId).OnDelete(DeleteBehavior.Cascade);
        }

        /// <summary>Phase 3 (inventory &amp; supply chain): transfers, adjustments, production.
        /// FK delete behaviour is NoAction into the Branches/InventoryItems/Users hubs; child
        /// lines cascade from their parent document.</summary>
        private void ConfigurePhase3Inventory(ModelBuilder modelBuilder)
        {
            // Stock transfers
            modelBuilder.Entity<StockTransfer>().HasOne(t => t.FromBranch).WithMany().HasForeignKey(t => t.FromBranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockTransfer>().HasOne(t => t.ToBranch).WithMany().HasForeignKey(t => t.ToBranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockTransfer>().HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockTransfer>().ToTable(t => t.HasCheckConstraint("CK_StockTransfer_Status", "[Status] IN ('Draft','Completed','Cancelled')"));
            modelBuilder.Entity<StockTransferItem>().HasOne(i => i.StockTransfer).WithMany(t => t.Items).HasForeignKey(i => i.StockTransferId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StockTransferItem>().HasOne(i => i.InventoryItem).WithMany().HasForeignKey(i => i.InventoryItemId).OnDelete(DeleteBehavior.NoAction);

            // Stock adjustments
            modelBuilder.Entity<StockAdjustment>().HasOne(a => a.Branch).WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockAdjustment>().HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockAdjustment>().HasOne(a => a.ApprovedBy).WithMany().HasForeignKey(a => a.ApprovedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockAdjustment>().ToTable(t =>
            {
                t.HasCheckConstraint("CK_StockAdjustment_Type", "[Type] IN ('Increase','Decrease','Recount')");
                t.HasCheckConstraint("CK_StockAdjustment_Approval", "[ApprovalStatus] IN ('Pending','Approved','Rejected')");
            });
            modelBuilder.Entity<StockAdjustmentLine>().HasOne(l => l.StockAdjustment).WithMany(a => a.Lines).HasForeignKey(l => l.StockAdjustmentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StockAdjustmentLine>().HasOne(l => l.InventoryItem).WithMany().HasForeignKey(l => l.InventoryItemId).OnDelete(DeleteBehavior.NoAction);

            // Production
            modelBuilder.Entity<ProductionOrder>().HasOne(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ProductionOrder>().HasOne(p => p.OutputItem).WithMany().HasForeignKey(p => p.OutputInventoryItemId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ProductionOrder>().HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ProductionOrder>().ToTable(t => t.HasCheckConstraint("CK_ProductionOrder_Status", "[Status] IN ('Draft','Completed','Cancelled')"));
            modelBuilder.Entity<ProductionInput>().HasOne(i => i.ProductionOrder).WithMany(p => p.Inputs).HasForeignKey(i => i.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ProductionInput>().HasOne(i => i.InventoryItem).WithMany().HasForeignKey(i => i.InventoryItemId).OnDelete(DeleteBehavior.NoAction);
        }

        /// <summary>Phase 2 (menu &amp; product depth): brands, units, modifiers, combos, price tiers.</summary>
        private void ConfigurePhase2Menu(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Brand>().HasIndex(b => new { b.TenantId, b.Name }).IsUnique();
            modelBuilder.Entity<PriceGroup>().HasIndex(p => new { p.TenantId, p.Name }).IsUnique();

            modelBuilder.Entity<Unit>().HasIndex(u => new { u.TenantId, u.Name }).IsUnique();
            modelBuilder.Entity<Unit>()
                .HasOne(u => u.BaseUnit).WithMany()
                .HasForeignKey(u => u.BaseUnitId).OnDelete(DeleteBehavior.NoAction);

            // Modifier groups → modifiers (cascade); junction to menu items.
            modelBuilder.Entity<Modifier>()
                .HasOne(m => m.Group).WithMany(g => g.Modifiers)
                .HasForeignKey(m => m.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MenuItemModifierGroup>()
                .HasIndex(x => new { x.TenantId, x.MenuItemId, x.ModifierGroupId }).IsUnique();
            modelBuilder.Entity<MenuItemModifierGroup>()
                .HasOne(x => x.MenuItem).WithMany(m => m.ModifierGroups)
                .HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MenuItemModifierGroup>()
                .HasOne(x => x.ModifierGroup).WithMany()
                .HasForeignKey(x => x.ModifierGroupId).OnDelete(DeleteBehavior.NoAction);

            // Combos → items (cascade); component menu items are NoAction (MenuItem is a hub).
            modelBuilder.Entity<Combo>()
                .HasOne(c => c.Branch).WithMany()
                .HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ComboItem>()
                .HasOne(c => c.Combo).WithMany(x => x.Items)
                .HasForeignKey(c => c.ComboId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ComboItem>()
                .HasOne(c => c.MenuItem).WithMany()
                .HasForeignKey(c => c.MenuItemId).OnDelete(DeleteBehavior.NoAction);

            // Tiered pricing per (item, group).
            modelBuilder.Entity<MenuItemPrice>()
                .HasIndex(p => new { p.TenantId, p.MenuItemId, p.PriceGroupId }).IsUnique();
            modelBuilder.Entity<MenuItemPrice>()
                .HasOne(p => p.MenuItem).WithMany(m => m.PriceOverrides)
                .HasForeignKey(p => p.MenuItemId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MenuItemPrice>()
                .HasOne(p => p.PriceGroup).WithMany()
                .HasForeignKey(p => p.PriceGroupId).OnDelete(DeleteBehavior.NoAction);

            // Default day mask = every day (127) so existing items stay available after migration.
            modelBuilder.Entity<MenuItem>().Property(m => m.AvailableDaysMask).HasDefaultValue(127);

            // MenuItem → brand/unit (optional).
            modelBuilder.Entity<MenuItem>()
                .HasOne(m => m.Brand).WithMany()
                .HasForeignKey(m => m.BrandId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<MenuItem>()
                .HasOne(m => m.Unit).WithMany()
                .HasForeignKey(m => m.UnitId).OnDelete(DeleteBehavior.SetNull);
        }

        /// <summary>
        /// Phase 1 (POS &amp; restaurant core): tables, split-payment tenders, and the new order
        /// service/kitchen fields. FK delete behaviour is NoAction/SetNull to avoid cascade paths
        /// into the Branches/Orders/Invoices hubs.
        /// </summary>
        private void ConfigurePhase1Pos(ModelBuilder modelBuilder)
        {
            // ── RestaurantTable ──
            modelBuilder.Entity<RestaurantTable>()
                .HasOne(t => t.Branch).WithMany()
                .HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<RestaurantTable>()
                .HasIndex(t => new { t.TenantId, t.BranchId, t.Name }).IsUnique();
            modelBuilder.Entity<RestaurantTable>()
                .ToTable(t => t.HasCheckConstraint("CK_RestaurantTable_Status",
                    "[Status] IN ('Available','Occupied','Reserved','Dirty')"));

            // ── Payment (split tenders) ── one invoice → many payments
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.InvoiceId);
            modelBuilder.Entity<Payment>()
                .ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "[Amount] > 0"));

            // ── Order: new Phase 1 relationships + guards ──
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Table).WithMany()
                .HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.ServiceStaff).WithMany()
                .HasForeignKey(o => o.ServiceStaffId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Order>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Order_ServiceType", "[ServiceType] IN ('DineIn','Takeaway','Delivery')");
                    t.HasCheckConstraint("CK_Order_KitchenStatus", "[KitchenStatus] IN ('New','Cooking','Ready','Served')");
                    t.HasCheckConstraint("CK_Order_HoldState", "[HoldState] IN ('Active','Suspended','Draft')");
                });
            // DB-level defaults so existing rows satisfy the new check constraints on migration,
            // and new inserts are valid even if a value is omitted.
            modelBuilder.Entity<Order>().Property(o => o.ServiceType).HasDefaultValue("DineIn");
            modelBuilder.Entity<Order>().Property(o => o.KitchenStatus).HasDefaultValue("New");
            modelBuilder.Entity<Order>().Property(o => o.HoldState).HasDefaultValue("Active");
            modelBuilder.Entity<RestaurantTable>().Property(t => t.Status).HasDefaultValue("Available");

            // Kitchen feed reads by (branch, kitchen status) — index it.
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.BranchId, o.KitchenStatus });

            // Barcode/SKU scan lookup — filtered unique index per tenant (SKU optional).
            modelBuilder.Entity<MenuItem>()
                .HasIndex(m => new { m.TenantId, m.Sku })
                .IsUnique()
                .HasFilter("[Sku] IS NOT NULL");
        }

        /// <summary>
        /// Wires tenant isolation for the whole model by convention so no entity is ever missed:
        ///  • a global query filter on every <see cref="ITenantOwned"/> entity (plus User/AuditLog),
        ///  • a NoAction FK + index on TenantId,
        ///  • the platform tables (Tenant/Plan/Subscription) themselves.
        /// </summary>
        private void ConfigureMultiTenancy(ModelBuilder modelBuilder)
        {
            // ── Platform tables ──
            modelBuilder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
            modelBuilder.Entity<Tenant>().HasIndex(t => t.CustomDomain)
                .IsUnique().HasFilter("[CustomDomain] IS NOT NULL");
            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.Plan).WithMany()
                .HasForeignKey(t => t.PlanId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Tenant>()
                .ToTable(t => t.HasCheckConstraint("CK_Tenant_Status",
                    "[Status] IN ('Active','Trial','Suspended')"));

            modelBuilder.Entity<Plan>().HasIndex(p => p.Name).IsUnique();
            modelBuilder.Entity<Plan>().Property(p => p.PriceMonthly).HasPrecision(10, 2);

            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Plan).WithMany()
                .HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Subscription>()
                .ToTable(t => t.HasCheckConstraint("CK_Subscription_Status",
                    "[Status] IN ('Active','Trialing','PastDue','Cancelled')"));

            // ── Convention pass: every ITenantOwned entity gets a filter, index and FK to Tenant ──
            var applyFilter = typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entityType.ClrType;
                if (clr == null || !typeof(ITenantOwned).IsAssignableFrom(clr)) continue;

                applyFilter.MakeGenericMethod(clr).Invoke(this, new object[] { modelBuilder });

                var builder = modelBuilder.Entity(clr);

                // Hot-path index: (TenantId, BranchId) when the entity is branch-scoped, else (TenantId).
                if (clr.GetProperty("BranchId") != null)
                    builder.HasIndex("TenantId", "BranchId");
                else
                    builder.HasIndex("TenantId");

                builder.HasOne(typeof(Tenant)).WithMany()
                    .HasForeignKey("TenantId").OnDelete(DeleteBehavior.NoAction);
            }

            // ── User & AuditLog: nullable TenantId (platform rows are tenant-less) ──
            modelBuilder.Entity<User>().HasQueryFilter(u => IgnoreTenantFilterFlag || u.TenantId == CurrentTenantIdValue);
            modelBuilder.Entity<User>().HasIndex(u => u.TenantId);
            modelBuilder.Entity<User>()
                .HasOne<Tenant>().WithMany()
                .HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<AuditLog>().HasQueryFilter(a => IgnoreTenantFilterFlag || a.TenantId == CurrentTenantIdValue);
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.TenantId);
            modelBuilder.Entity<AuditLog>()
                .HasOne<Tenant>().WithMany()
                .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.NoAction);
        }

        /// <summary>Applies the tenant query filter for one entity type. Referencing the context
        /// members keeps the filter live (EF parameterizes them) while the model stays cached.</summary>
        private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned
        {
            modelBuilder.Entity<TEntity>()
                .HasQueryFilter(e => IgnoreTenantFilterFlag || e.TenantId == CurrentTenantIdValue);
        }

        /// <summary>
        /// Promo / Partnership / Invoice / BranchSetting configuration. All FK delete
        /// behaviours are NoAction/SetNull on purpose — these tables reference Branches,
        /// Orders and Users, and SQL Server rejects multiple cascade paths into those hubs.
        /// </summary>
        private void ConfigureCheckoutModules(ModelBuilder modelBuilder)
        {
            // ── PromoCode ── (code is unique per tenant, not globally)
            modelBuilder.Entity<PromoCode>()
                .HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            modelBuilder.Entity<PromoCode>()
                .ToTable(t => t.HasCheckConstraint("CK_PromoCode_DiscountType",
                    "[DiscountType] IN ('Percentage','Flat')"));
            modelBuilder.Entity<PromoCode>()
                .HasOne(p => p.Branch).WithMany()
                .HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<PromoCode>()
                .HasOne(p => p.CreatedBy).WithMany()
                .HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.SetNull);

            // ── Partnership ──
            modelBuilder.Entity<Partnership>()
                .HasOne(p => p.Branch).WithMany()
                .HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Partnership>()
                .HasOne(p => p.CreatedBy).WithMany()
                .HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.SetNull);

            // ── Invoice (one per order) ──
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.OrderId).IsUnique();
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
            modelBuilder.Entity<Invoice>()
                .ToTable(t => t.HasCheckConstraint("CK_Invoice_PaymentStatus",
                    "[PaymentStatus] IN ('Pending','Paid','Failed','Cancelled')"));
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Order).WithOne(o => o.Invoice)
                .HasForeignKey<Invoice>(i => i.OrderId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Branch).WithMany()
                .HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.PromoCode).WithMany()
                .HasForeignKey(i => i.PromoCodeId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Partnership).WithMany()
                .HasForeignKey(i => i.PartnershipId).OnDelete(DeleteBehavior.SetNull);

            // ── BranchSetting (one per branch) ──
            modelBuilder.Entity<BranchSetting>()
                .HasIndex(b => b.BranchId).IsUnique();
            modelBuilder.Entity<BranchSetting>()
                .HasOne(b => b.Branch).WithMany()
                .HasForeignKey(b => b.BranchId).OnDelete(DeleteBehavior.NoAction);
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
                .Property(i => i.SellingCost)
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
            // Email stays GLOBALLY unique — login resolves a user by email across tenants.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ── Natural keys are unique PER TENANT (multi-tenancy): each tenant has its own
            //    order/employee/category/ingredient namespace, so these are composite with TenantId. ──
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.OrderNumber })
                .IsUnique();

            modelBuilder.Entity<Staff>()
                .HasIndex(s => new { s.TenantId, s.EmployeeId })
                .IsUnique()
                .HasFilter("[EmployeeId] IS NOT NULL");

            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.TenantId, c.Name })
                .IsUnique();

            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => new { i.TenantId, i.Name })
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

            // NotificationPreference: one per user
            modelBuilder.Entity<NotificationPreference>()
                .HasIndex(np => np.UserId)
                .IsUnique();
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

            // CustomerId is nullable (Phase 1 walk-in sales); deleting a customer must NOT delete
            // the sale — null the link so revenue history is preserved.
            modelBuilder.Entity<User>()
                .HasMany(u => u.Orders)
                .WithOne(o => o.Customer)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

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

            // Supplier relationships
            modelBuilder.Entity<Supplier>()
                .HasOne(s => s.Branch)
                .WithMany()
                .HasForeignKey(s => s.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.Supplier)
                .WithMany(s => s.InventoryItems)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Branch)
                .WithMany()
                .HasForeignKey(p => p.BranchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
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

            // >= 0 (not > 0): Phase 1 drafts/held carts can sit at 0 until finalised.
            modelBuilder.Entity<Order>()
                .ToTable(t => t.HasCheckConstraint("CK_Order_TotalAmount",
                    "[TotalAmount] >= 0"));

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
                    "[Status] IN ('Present','Absent','Late','Half-Day','Paid Leave','Sick Leave','Casual Leave','Holiday','Work From Home','Overtime')"));

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

        private void ConfigureNotifications(ModelBuilder modelBuilder)
        {
            // Notification indexes for performance
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.UserId);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.IsRead);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.CreatedAt);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.RoleTarget);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.BranchId);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.Type);

            // Notification relationships - prevent cascade cycles
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Branch)
                .WithMany()
                .HasForeignKey(n => n.BranchId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Creator)
                .WithMany()
                .HasForeignKey(n => n.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            // EmailQueue indexes
            modelBuilder.Entity<EmailQueue>()
                .HasIndex(e => e.IsSent);
            modelBuilder.Entity<EmailQueue>()
                .HasIndex(e => e.CreatedAt);

            modelBuilder.Entity<EmailQueue>()
                .HasOne(e => e.Notification)
                .WithMany()
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.SetNull);

            // NotificationPreference relationship
            modelBuilder.Entity<NotificationPreference>()
                .HasOne(np => np.User)
                .WithMany()
                .HasForeignKey(np => np.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}