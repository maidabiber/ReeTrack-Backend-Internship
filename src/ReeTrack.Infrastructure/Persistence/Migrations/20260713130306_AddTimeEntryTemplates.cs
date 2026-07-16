using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeEntryTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "time_entry_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    start_time_utc = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time_utc = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_entry_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_time_entry_templates_project_tasks_project_task_id",
                        column: x => x.project_task_id,
                        principalTable: "project_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_time_entry_templates_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_time_entry_templates_time_entries_time_entry_id",
                        column: x => x.time_entry_id,
                        principalTable: "time_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_time_entry_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_time_entry_templates_project_id",
                table: "time_entry_templates",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_time_entry_templates_project_task_id",
                table: "time_entry_templates",
                column: "project_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_time_entry_templates_time_entry_id",
                table: "time_entry_templates",
                column: "time_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_entry_templates_user_id",
                table: "time_entry_templates",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "time_entry_templates");
        }
    }
}
