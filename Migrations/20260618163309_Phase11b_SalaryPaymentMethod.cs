using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class Phase11b_SalaryPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Branches_BranchId",
                table: "Purchases");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "SalaryRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentNotes",
                table: "SalaryRecords",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "SalaryRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Purchases",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Branches_BranchId",
                table: "Purchases",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Branches_BranchId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "PaymentNotes",
                table: "SalaryRecords");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "SalaryRecords");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Purchases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Branches_BranchId",
                table: "Purchases",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
