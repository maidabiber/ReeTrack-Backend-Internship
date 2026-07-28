using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "projects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_key",
                table: "projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "external_provider",
                table: "projects",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "project_tasks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_key",
                table: "project_tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "external_provider",
                table: "project_tasks",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_external_provider_external_id",
                table: "projects",
                columns: new[] { "external_provider", "external_id" },
                unique: true,
                filter: "deleted_at_utc IS NULL AND external_provider IS NOT NULL AND external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_external_provider_external_id",
                table: "project_tasks",
                columns: new[] { "external_provider", "external_id" },
                unique: true,
                filter: "deleted_at_utc IS NULL AND external_provider IS NOT NULL AND external_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_external_provider_external_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_project_tasks_external_provider_external_id",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "external_key",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "external_provider",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "external_key",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "external_provider",
                table: "project_tasks");
        }
    }
}
