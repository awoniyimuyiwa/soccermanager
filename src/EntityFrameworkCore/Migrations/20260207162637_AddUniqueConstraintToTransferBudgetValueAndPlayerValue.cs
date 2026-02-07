using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToTransferBudgetValueAndPlayerValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferBudgetValues_TransferId",
                table: "TransferBudgetValues");

            migrationBuilder.CreateIndex(
                name: "IX_TransferBudgetValues_TransferId",
                table: "TransferBudgetValues",
                column: "TransferId",
                unique: true,
                filter: "[TransferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerValues_Type_SourceEntityId",
                table: "PlayerValues",
                columns: new[] { "Type", "SourceEntityId" },
                unique: true,
                filter: "[SourceEntityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferBudgetValues_TransferId",
                table: "TransferBudgetValues");

            migrationBuilder.DropIndex(
                name: "IX_PlayerValues_Type_SourceEntityId",
                table: "PlayerValues");

            migrationBuilder.CreateIndex(
                name: "IX_TransferBudgetValues_TransferId",
                table: "TransferBudgetValues",
                column: "TransferId");
        }
    }
}
