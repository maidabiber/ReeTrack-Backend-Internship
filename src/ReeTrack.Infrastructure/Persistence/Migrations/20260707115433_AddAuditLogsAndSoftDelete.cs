using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogsAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_time_entries_user_running",
                table: "time_entries");

            migrationBuilder.DropIndex(
                name: "ix_tags_name",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_clients_name",
                table: "clients");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "time_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "time_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "tags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "tags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "project_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "project_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<short>(type: "smallint", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_user_running",
                table: "time_entries",
                column: "user_id",
                unique: true,
                filter: "mode = 0 AND ended_at_utc IS NULL AND deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tags_name",
                table: "tags",
                column: "name",
                unique: true,
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clients_name",
                table: "clients",
                column: "name",
                unique: true,
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at_utc",
                table: "audit_logs",
                column: "occurred_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_time_entries_user_running",
                table: "time_entries");

            migrationBuilder.DropIndex(
                name: "ix_tags_name",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_clients_name",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_user_running",
                table: "time_entries",
                column: "user_id",
                unique: true,
                filter: "mode = 0 AND ended_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_name",
                table: "clients",
                column: "name",
                unique: true);
        }
    }
}
