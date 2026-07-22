using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReeTrack.Infrastructure.Persistence;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722101500_AddProjectTaskCostSnapshots")]
public partial class AddProjectTaskCostSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_task_cost_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                project_cost_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                calculated_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                total_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                weekend_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                holiday_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                overtime_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_task_cost_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_project_task_cost_snapshots_project_cost_snapshots_project_cost_snapshot_id",
                    column: x => x.project_cost_snapshot_id,
                    principalTable: "project_cost_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_project_task_cost_snapshots_project_tasks_project_task_id",
                    column: x => x.project_task_id,
                    principalTable: "project_tasks",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_project_task_cost_snapshots_project_cost_snapshot_id",
            table: "project_task_cost_snapshots",
            column: "project_cost_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "ix_project_task_cost_snapshots_project_task_id",
            table: "project_task_cost_snapshots",
            column: "project_task_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "project_task_cost_snapshots");
    }
}
