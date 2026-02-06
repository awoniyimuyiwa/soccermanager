using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultValuesForDecimalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "TransferBudgetValues",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Teams",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferBudget",
                table: "Teams",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "PlayerValues",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Players",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "TransferBudgetValues",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Teams",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferBudget",
                table: "Teams",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "PlayerValues",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Players",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldDefaultValue: 0m);
        }
    }
}
