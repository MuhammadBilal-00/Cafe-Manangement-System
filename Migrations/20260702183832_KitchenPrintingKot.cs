using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class KitchenPrintingKot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KotStation",
                table: "Categories",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintKot",
                table: "BranchSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "KitchenPrinters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ConnectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: false),
                    Station = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenPrinters", x => x.Id);
                    table.CheckConstraint("CK_KitchenPrinter_ConnType", "[ConnectionType] IN ('Network','Browser')");
                    table.ForeignKey(
                        name: "FK_KitchenPrinters_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KitchenPrinters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KotPrintLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    KitchenPrinterId = table.Column<int>(type: "int", nullable: true),
                    Station = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PrinterName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotPrintLogs", x => x.Id);
                    table.CheckConstraint("CK_KotPrintLog_Status", "[Status] IN ('Printed','Browser','Queued','Failed','Test')");
                    table.ForeignKey(
                        name: "FK_KotPrintLogs_KitchenPrinters_KitchenPrinterId",
                        column: x => x.KitchenPrinterId,
                        principalTable: "KitchenPrinters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KotPrintLogs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KotPrintLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenPrinters_BranchId",
                table: "KitchenPrinters",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenPrinters_TenantId_BranchId",
                table: "KitchenPrinters",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenPrinters_TenantId_BranchId_IsActive",
                table: "KitchenPrinters",
                columns: new[] { "TenantId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_KotPrintLogs_KitchenPrinterId",
                table: "KotPrintLogs",
                column: "KitchenPrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_KotPrintLogs_OrderId",
                table: "KotPrintLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_KotPrintLogs_TenantId",
                table: "KotPrintLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KotPrintLogs_TenantId_OrderId",
                table: "KotPrintLogs",
                columns: new[] { "TenantId", "OrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KotPrintLogs");

            migrationBuilder.DropTable(
                name: "KitchenPrinters");

            migrationBuilder.DropColumn(
                name: "KotStation",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AutoPrintKot",
                table: "BranchSettings");
        }
    }
}
