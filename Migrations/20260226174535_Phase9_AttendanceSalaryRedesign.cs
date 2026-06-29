using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_AttendanceSalaryRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbsenceDeduction",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AttendanceBonus",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "SalaryRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalizedById",
                table: "SalaryRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossSalary",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HalfDayDeduction",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LatePenaltyDeduction",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHours",
                table: "SalaryRecords",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimePay",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SalaryRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeductions",
                table: "SalaryRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedAt",
                table: "SalaryRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnlockedById",
                table: "SalaryRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHours",
                table: "Attendances",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalHours",
                table: "Attendances",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SalaryAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryRecordId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdjustments", x => x.Id);
                    table.CheckConstraint("CK_SalaryAdjustment_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_SalaryAdjustment_Type", "[Type] IN ('Bonus','Deduction')");
                    table.ForeignKey(
                        name: "FK_SalaryAdjustments_SalaryRecords_SalaryRecordId",
                        column: x => x.SalaryRecordId,
                        principalTable: "SalaryRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_SalaryAdjustments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_FinalizedById",
                table: "SalaryRecords",
                column: "FinalizedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRecords_UnlockedById",
                table: "SalaryRecords",
                column: "UnlockedById");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords",
                sql: "[Status] IN ('Draft','Finalized')");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_CreatedById",
                table: "SalaryAdjustments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_SalaryRecordId",
                table: "SalaryAdjustments",
                column: "SalaryRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryRecords_Users_FinalizedById",
                table: "SalaryRecords",
                column: "FinalizedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryRecords_Users_UnlockedById",
                table: "SalaryRecords",
                column: "UnlockedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalaryRecords_Users_FinalizedById",
                table: "SalaryRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryRecords_Users_UnlockedById",
                table: "SalaryRecords");

            migrationBuilder.DropTable(
                name: "SalaryAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_FinalizedById",
                table: "SalaryRecords");

            migrationBuilder.DropIndex(
                name: "IX_SalaryRecords_UnlockedById",
                table: "SalaryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryRecord_Status",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "AbsenceDeduction",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "AttendanceBonus",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "FinalizedById",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "GrossSalary",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "HalfDayDeduction",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "LatePenaltyDeduction",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "OvertimeHours",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "OvertimePay",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "TotalDeductions",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "UnlockedAt",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "UnlockedById",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "OvertimeHours",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "TotalHours",
                table: "Attendances");
        }
    }
}
