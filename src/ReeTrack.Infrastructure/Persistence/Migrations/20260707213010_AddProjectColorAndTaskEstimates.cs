using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectColorAndTaskEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "projects",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "time_estimate_hours",
                table: "project_tasks",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_name",
                table: "projects",
                column: "name",
                unique: true,
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_project_id_name",
                table: "project_tasks",
                columns: new[] { "project_id", "name" },
                unique: true,
                filter: "deleted_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_name",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_project_tasks_project_id_name",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "color",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "time_estimate_hours",
                table: "project_tasks");
        }
    }
}
