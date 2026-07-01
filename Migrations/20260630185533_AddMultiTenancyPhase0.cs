using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancyPhase0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TodoItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StaffSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StaffSalaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StaffRoles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Staff",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SalesReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SalaryRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SalaryPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SalaryAdjustments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Purchases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PromoCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Partnerships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "NotificationPreferences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MenuItemReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MenuItemIngredients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryRecipeMappings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Ingredients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EmailQueues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DailySpecials",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BranchSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Attendances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PriceMonthly = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxBranches = table.Column<int>(type: "int", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    Features = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    CustomDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: true),
                    BrandingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.CheckConstraint("CK_Tenant_Status", "[Status] IN ('Active','Trial','Suspended')");
                    table.ForeignKey(
                        name: "FK_Tenants_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.CheckConstraint("CK_Subscription_Status", "[Status] IN ('Active','Trialing','PastDue','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Subscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // ── Phase 0 retrofit: backfill existing data to a single "Demo" tenant ──
            // Runs ONLY when the database already contains rows (an existing install). It creates
            // the Demo tenant and points every existing row at it, so the NOT NULL TenantId columns
            // (added above with default 0) hold valid values BEFORE the foreign keys below are
            // created. On a fresh database this whole block is a no-op and SeedData owns seeding.
            // PlanId is left null here; SeedData assigns the Demo tenant the "Pro" plan on startup
            // so the existing install keeps access to every module.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Users])
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Tenants] WHERE [Slug] = 'demo')
        INSERT INTO [Tenants] ([Name],[Slug],[Status],[CreatedAt])
        VALUES (N'Demo Cafe Co.', 'demo', 'Active', SYSUTCDATETIME());

    DECLARE @demoId INT = (SELECT TOP 1 [Id] FROM [Tenants] WHERE [Slug] = 'demo');

    UPDATE [Attendances]             SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [BranchSettings]          SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Branches]                SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Categories]              SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Customers]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [DailySpecials]           SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [EmailQueues]             SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Expenses]                SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Feedbacks]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Ingredients]             SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [InventoryItems]          SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [InventoryRecipeMappings] SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [InventoryTransactions]   SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Invoices]                SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [MenuItemIngredients]     SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [MenuItemReviews]         SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [MenuItems]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [NotificationPreferences] SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Notifications]           SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [OrderItems]              SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Orders]                  SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Partnerships]            SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [PromoCodes]              SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Purchases]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [SalaryAdjustments]       SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [SalaryPolicies]          SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [SalaryRecords]           SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [SalesReports]            SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Staff]                   SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [StaffRoles]              SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [StaffSalaries]           SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [StaffSchedules]          SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Suppliers]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [TodoItems]               SET [TenantId] = @demoId WHERE [TenantId] = 0;
    UPDATE [Users]                   SET [TenantId] = @demoId WHERE [TenantId] IS NULL;
    UPDATE [AuditLogs]               SET [TenantId] = @demoId WHERE [TenantId] IS NULL;
END
");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_TenantId",
                table: "TodoItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_BranchId",
                table: "Suppliers",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffSchedules_TenantId",
                table: "StaffSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffSalaries_TenantId",
                table: "StaffSalaries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_TenantId",
                table: "StaffRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_TenantId_BranchId",
                table: "Staff",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_TenantId_BranchId",
                table: "SalesReports",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_TenantId_BranchId",
                table: "SalaryRecords",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPolicies_TenantId",
                table: "SalaryPolicies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_TenantId",
                table: "SalaryAdjustments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId_BranchId",
                table: "Purchases",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_TenantId_BranchId",
                table: "PromoCodes",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Partnerships_TenantId_BranchId",
                table: "Partnerships",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_BranchId",
                table: "Notifications",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_TenantId",
                table: "NotificationPreferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_BranchId",
                table: "MenuItems",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemReviews_TenantId",
                table: "MenuItemReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemIngredients_TenantId",
                table: "MenuItemIngredients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_BranchId",
                table: "Invoices",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_BranchId",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecipeMappings_TenantId",
                table: "InventoryRecipeMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_BranchId",
                table: "InventoryItems",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId",
                table: "Ingredients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_TenantId_BranchId",
                table: "Feedbacks",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId_BranchId",
                table: "Expenses",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueues_TenantId",
                table: "EmailQueues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySpecials_TenantId_BranchId",
                table: "DailySpecials",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId",
                table: "Categories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchSettings_TenantId_BranchId",
                table: "BranchSettings",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId",
                table: "Branches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_TenantId_BranchId",
                table: "Attendances",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Name",
                table: "Plans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CustomDomain",
                table: "Tenants",
                column: "CustomDomain",
                unique: true,
                filter: "[CustomDomain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Tenants_TenantId",
                table: "Attendances",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                table: "Branches",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BranchSettings_Tenants_TenantId",
                table: "BranchSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Tenants_TenantId",
                table: "Categories",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                table: "Customers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DailySpecials_Tenants_TenantId",
                table: "DailySpecials",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailQueues_Tenants_TenantId",
                table: "EmailQueues",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Tenants_TenantId",
                table: "Feedbacks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Tenants_TenantId",
                table: "Ingredients",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Tenants_TenantId",
                table: "InventoryItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryRecipeMappings_Tenants_TenantId",
                table: "InventoryRecipeMappings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Tenants_TenantId",
                table: "InventoryTransactions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemIngredients_Tenants_TenantId",
                table: "MenuItemIngredients",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemReviews_Tenants_TenantId",
                table: "MenuItemReviews",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationPreferences_Tenants_TenantId",
                table: "NotificationPreferences",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Tenants_TenantId",
                table: "Notifications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Tenants_TenantId",
                table: "OrderItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tenants_TenantId",
                table: "Orders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Partnerships_Tenants_TenantId",
                table: "Partnerships",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PromoCodes_Tenants_TenantId",
                table: "PromoCodes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Tenants_TenantId",
                table: "Purchases",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryAdjustments_Tenants_TenantId",
                table: "SalaryAdjustments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryPolicies_Tenants_TenantId",
                table: "SalaryPolicies",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryRecords_Tenants_TenantId",
                table: "SalaryRecords",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReports_Tenants_TenantId",
                table: "SalesReports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Staff_Tenants_TenantId",
                table: "Staff",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffRoles_Tenants_TenantId",
                table: "StaffRoles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffSalaries_Tenants_TenantId",
                table: "StaffSalaries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffSchedules_Tenants_TenantId",
                table: "StaffSchedules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Tenants_TenantId",
                table: "Suppliers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TodoItems_Tenants_TenantId",
                table: "TodoItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Tenants_TenantId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchSettings_Tenants_TenantId",
                table: "BranchSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Tenants_TenantId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_DailySpecials_Tenants_TenantId",
                table: "DailySpecials");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailQueues_Tenants_TenantId",
                table: "EmailQueues");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Tenants_TenantId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Tenants_TenantId",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Tenants_TenantId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryRecipeMappings_Tenants_TenantId",
                table: "InventoryRecipeMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Tenants_TenantId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemIngredients_Tenants_TenantId",
                table: "MenuItemIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemReviews_Tenants_TenantId",
                table: "MenuItemReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationPreferences_Tenants_TenantId",
                table: "NotificationPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Tenants_TenantId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Tenants_TenantId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tenants_TenantId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Partnerships_Tenants_TenantId",
                table: "Partnerships");

            migrationBuilder.DropForeignKey(
                name: "FK_PromoCodes_Tenants_TenantId",
                table: "PromoCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Tenants_TenantId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryAdjustments_Tenants_TenantId",
                table: "SalaryAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryPolicies_Tenants_TenantId",
                table: "SalaryPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryRecords_Tenants_TenantId",
                table: "SalaryRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReports_Tenants_TenantId",
                table: "SalesReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Staff_Tenants_TenantId",
                table: "Staff");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffRoles_Tenants_TenantId",
                table: "StaffRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffSalaries_Tenants_TenantId",
                table: "StaffSalaries");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffSchedules_Tenants_TenantId",
                table: "StaffSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Tenants_TenantId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_TodoItems_Tenants_TenantId",
                table: "TodoItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TodoItems_TenantId",
                table: "TodoItems");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_BranchId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StaffSchedules_TenantId",
                table: "StaffSchedules");

            migrationBuilder.DropIndex(
                name: "IX_StaffSalaries_TenantId",
                table: "StaffSalaries");

            migrationBuilder.DropIndex(
                name: "IX_StaffRoles_TenantId",
                table: "StaffRoles");

            migrationBuilder.DropIndex(
                name: "IX_Staff_TenantId_BranchId",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_SalesReports_TenantId_BranchId",
                table: "SalesReports");

            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_TenantId_BranchId",
                table: "SalaryRecords");

            migrationBuilder.DropIndex(
                name: "IX_SalaryPolicies_TenantId",
                table: "SalaryPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SalaryAdjustments_TenantId",
                table: "SalaryAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TenantId_BranchId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodes_TenantId_BranchId",
                table: "PromoCodes");

            migrationBuilder.DropIndex(
                name: "IX_Partnerships_TenantId_BranchId",
                table: "Partnerships");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_BranchId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_TenantId",
                table: "NotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId_BranchId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItemReviews_TenantId",
                table: "MenuItemReviews");

            migrationBuilder.DropIndex(
                name: "IX_MenuItemIngredients_TenantId",
                table: "MenuItemIngredients");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_BranchId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_TenantId_BranchId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryRecipeMappings_TenantId",
                table: "InventoryRecipeMappings");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_TenantId_BranchId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_TenantId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_TenantId_BranchId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantId_BranchId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_EmailQueues_TenantId",
                table: "EmailQueues");

            migrationBuilder.DropIndex(
                name: "IX_DailySpecials_TenantId_BranchId",
                table: "DailySpecials");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_BranchSettings_TenantId_BranchId",
                table: "BranchSettings");

            migrationBuilder.DropIndex(
                name: "IX_Branches_TenantId",
                table: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_TenantId_BranchId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TodoItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StaffSchedules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StaffSalaries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StaffRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SalaryPolicies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SalaryAdjustments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Partnerships");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItemReviews");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItemIngredients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryRecipeMappings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmailQueues");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DailySpecials");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BranchSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Attendances");
        }
    }
}
