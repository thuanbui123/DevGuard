using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DevGuard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScanHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScanHistoryId",
                table: "Issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ScanHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepositoryPath = table.Column<string>(type: "text", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalIssues = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ScanHistoryId",
                table: "Issues",
                column: "ScanHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_ScanHistories_ScanHistoryId",
                table: "Issues",
                column: "ScanHistoryId",
                principalTable: "ScanHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_ScanHistories_ScanHistoryId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "ScanHistories");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ScanHistoryId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ScanHistoryId",
                table: "Issues");
        }
    }
}
