using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectCreatorAndBillingCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // created_by_user_id is non-nullable with no meaningful backfill:
            // existing projects predate creator tracking, so wipe them (and their
            // tasks, which FK-Restrict against projects). Time entries survive —
            // their project/task FKs are ON DELETE SET NULL.
            migrationBuilder.Sql("DELETE FROM project_tasks;");
            migrationBuilder.Sql("DELETE FROM projects;");

            migrationBuilder.DropColumn(
                name: "billing_type",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "budget_amount",
                table: "projects");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "projects");

            migrationBuilder.AddColumn<short>(
                name: "billing_type",
                table: "projects",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "budget_amount",
                table: "projects",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
