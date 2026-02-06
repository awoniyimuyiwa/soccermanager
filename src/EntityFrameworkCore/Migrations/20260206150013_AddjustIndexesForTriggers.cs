using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddjustIndexesForTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferBudgetValues_TeamId",
                table: "TransferBudgetValues");

            migrationBuilder.DropIndex(
                name: "IX_PlayerValues_PlayerId",
                table: "PlayerValues");

            migrationBuilder.DropIndex(
                name: "IX_Players_TeamId",
                table: "Players");

            migrationBuilder.CreateIndex(
                name: "IX_TransferBudgetValues_TeamId",
                table: "TransferBudgetValues",
                column: "TeamId")
                .Annotation("SqlServer:Include", new[] { "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerValues_PlayerId",
                table: "PlayerValues",
                column: "PlayerId")
                .Annotation("SqlServer:Include", new[] { "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId")
                .Annotation("SqlServer:Include", new[] { "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferBudgetValues_TeamId",
                table: "TransferBudgetValues");

            migrationBuilder.DropIndex(
                name: "IX_PlayerValues_PlayerId",
                table: "PlayerValues");

            migrationBuilder.DropIndex(
                name: "IX_Players_TeamId",
                table: "Players");

            migrationBuilder.CreateIndex(
                name: "IX_TransferBudgetValues_TeamId",
                table: "TransferBudgetValues",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerValues_PlayerId",
                table: "PlayerValues",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");
        }
    }
}
