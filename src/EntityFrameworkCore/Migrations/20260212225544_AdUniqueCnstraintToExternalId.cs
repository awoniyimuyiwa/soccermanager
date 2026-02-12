using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AdUniqueCnstraintToExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BackgroundServiceStats_ExternalId",
                table: "BackgroundServiceStats",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ExternalId",
                table: "AuditLogs",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundServiceStats_ExternalId",
                table: "BackgroundServiceStats");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ExternalId",
                table: "AuditLogs");
        }
    }
}
