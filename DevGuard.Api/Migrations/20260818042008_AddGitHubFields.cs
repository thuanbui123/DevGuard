using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevGuard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubToken",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoName",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoOwner",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubToken",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RepoName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RepoOwner",
                table: "Projects");
        }
    }
}
