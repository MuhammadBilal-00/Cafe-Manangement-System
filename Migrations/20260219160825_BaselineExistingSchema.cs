using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class BaselineExistingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Feedbacks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Feedbacks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Feedbacks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Feedbacks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffNote",
                table: "Feedbacks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Feedbacks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Tables already created by InventoryManagementModule — skip if they exist
            migrationBuilder.Sql(@"
IF OBJECT_ID('InventoryRecipeMappings') IS NULL
BEGIN
    CREATE TABLE [InventoryRecipeMappings] (
        [Id] int NOT NULL IDENTITY,
        [MenuItemId] int NOT NULL,
        [InventoryItemId] int NOT NULL,
        [QuantityRequired] decimal(10,2) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_InventoryRecipeMappings] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_InventoryRecipeMappings_InventoryItemId] ON [InventoryRecipeMappings] ([InventoryItemId]);
    CREATE INDEX [IX_InventoryRecipeMappings_MenuItemId] ON [InventoryRecipeMappings] ([MenuItemId]);
END");

            migrationBuilder.Sql(@"
IF OBJECT_ID('InventoryTransactions') IS NULL
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [Id] int NOT NULL IDENTITY,
        [InventoryItemId] int NOT NULL,
        [TransactionType] nvarchar(20) NOT NULL,
        [Quantity] decimal(10,2) NOT NULL,
        [QuantityBefore] decimal(10,2) NOT NULL,
        [QuantityAfter] decimal(10,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [TransactionDate] datetime2 NOT NULL DEFAULT GETDATE(),
        [BranchId] int NOT NULL,
        [OrderId] int NULL,
        [PerformedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_InventoryTransactions_BranchId] ON [InventoryTransactions] ([BranchId]);
    CREATE INDEX [IX_InventoryTransactions_InventoryItemId] ON [InventoryTransactions] ([InventoryItemId]);
    CREATE INDEX [IX_InventoryTransactions_OrderId] ON [InventoryTransactions] ([OrderId]);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Feedbacks_OrderId' AND object_id=OBJECT_ID('Feedbacks'))
    CREATE INDEX [IX_Feedbacks_OrderId] ON [Feedbacks] ([OrderId]);");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Orders_OrderId",
                table: "Feedbacks",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Orders_OrderId",
                table: "Feedbacks");

            migrationBuilder.DropTable(
                name: "InventoryRecipeMappings");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_OrderId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "StaffNote",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Feedbacks");
        }
    }
}
