using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToExternalIdOnAllTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ExternalId",
                table: "Transfers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferBudgetValues_ExternalId",
                table: "TransferBudgetValues",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalId",
                table: "Teams",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerValues_ExternalId",
                table: "PlayerValues",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_ExternalId",
                table: "Players",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ExternalId",
                table: "AuditLog",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ExternalId",
                table: "AspNetUsers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoles_ExternalId",
                table: "AspNetRoles",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transfers_ExternalId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_TransferBudgetValues_ExternalId",
                table: "TransferBudgetValues");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ExternalId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_PlayerValues_ExternalId",
                table: "PlayerValues");

            migrationBuilder.DropIndex(
                name: "IX_Players_ExternalId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_ExternalId",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ExternalId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetRoles_ExternalId",
                table: "AspNetRoles");
        }
    }
}
