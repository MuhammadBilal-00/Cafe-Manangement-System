using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_SalaryPolicyVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_BranchId",
                table: "SalaryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords");

            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "StaffSalaries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyIdUsed",
                table: "SalaryRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalaryPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AbsenceDeductionFactor = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    HalfDayDeductionFactor = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LatePenaltyThreshold = table.Column<int>(type: "int", nullable: false),
                    LatePenaltyFactor = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OvertimeMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AttendanceBonusPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxLateForBonus = table.Column<int>(type: "int", nullable: false),
                    MaxAbsentForBonus = table.Column<int>(type: "int", nullable: false),
                    StandardDailyHours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LateThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryPolicies_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalaryPolicies_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_BranchId_Year_Month",
                table: "SalaryRecords",
                columns: new[] { "BranchId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_PolicyIdUsed",
                table: "SalaryRecords",
                column: "PolicyIdUsed");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords",
                sql: "[Status] IN ('Draft','Finalized','Paid')");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPolicies_CreatedById",
                table: "SalaryPolicies",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryPolicies_UpdatedById",
                table: "SalaryPolicies",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryRecords_SalaryPolicies_PolicyIdUsed",
                table: "SalaryRecords",
                column: "PolicyIdUsed",
                principalTable: "SalaryPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalaryRecords_SalaryPolicies_PolicyIdUsed",
                table: "SalaryRecords");

            migrationBuilder.DropTable(
                name: "SalaryPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_BranchId_Year_Month",
                table: "SalaryRecords");

            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_PolicyIdUsed",
                table: "SalaryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "StaffSalaries");

            migrationBuilder.DropColumn(
                name: "PolicyIdUsed",
                table: "SalaryRecords");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_BranchId",
                table: "SalaryRecords",
                column: "BranchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords",
                sql: "[Status] IN ('Draft','Finalized')");
        }
    }
}
