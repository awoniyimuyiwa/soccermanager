using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceIdAndTraceIdToBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "BackgroundJobs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "BackgroundJobs");
        }
    }
}
