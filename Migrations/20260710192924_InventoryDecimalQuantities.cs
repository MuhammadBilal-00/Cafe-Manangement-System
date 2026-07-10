using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class InventoryDecimalQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server refuses ALTER COLUMN while a check constraint references the column,
            // so the CKs come off first and are re-created after the widening. Drops are
            // conditional: databases migrated during the old "reconciliation loop" era are
            // missing these two CKs (model/DB drift) — re-creating them below repairs that.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_InventoryItem_Quantity') ALTER TABLE [InventoryItems] DROP CONSTRAINT [CK_InventoryItem_Quantity];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_InventoryItem_ReorderLevel') ALTER TABLE [InventoryItems] DROP CONSTRAINT [CK_InventoryItem_ReorderLevel];");
            // Guard against pre-existing bad rows so the WITH CHECK re-add below can't fail.
            migrationBuilder.Sql("UPDATE [InventoryItems] SET [Quantity] = 0 WHERE [Quantity] < 0; UPDATE [InventoryItems] SET [ReorderLevel] = 0 WHERE [ReorderLevel] < 0;");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReorderLevel",
                table: "InventoryItems",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(int),
                oldType: "int",
                oldPrecision: 10,
                oldScale: 2,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryItems",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(int),
                oldType: "int",
                oldPrecision: 10,
                oldScale: 2,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumStock",
                table: "InventoryItems",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql("ALTER TABLE [InventoryItems] ADD CONSTRAINT [CK_InventoryItem_Quantity] CHECK ([Quantity] >= 0);");
            migrationBuilder.Sql("ALTER TABLE [InventoryItems] ADD CONSTRAINT [CK_InventoryItem_ReorderLevel] CHECK ([ReorderLevel] >= 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [InventoryItems] DROP CONSTRAINT [CK_InventoryItem_Quantity];");
            migrationBuilder.Sql("ALTER TABLE [InventoryItems] DROP CONSTRAINT [CK_InventoryItem_ReorderLevel];");

            migrationBuilder.AlterColumn<int>(
                name: "ReorderLevel",
                table: "InventoryItems",
                type: "int",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "InventoryItems",
                type: "int",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "MinimumStock",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.Sql("ALTER TABLE [InventoryItems] ADD CONSTRAINT [CK_InventoryItem_Quantity] CHECK ([Quantity] >= 0);");
            migrationBuilder.Sql("ALTER TABLE [InventoryItems] ADD CONSTRAINT [CK_InventoryItem_ReorderLevel] CHECK ([ReorderLevel] >= 0);");
        }
    }
}
