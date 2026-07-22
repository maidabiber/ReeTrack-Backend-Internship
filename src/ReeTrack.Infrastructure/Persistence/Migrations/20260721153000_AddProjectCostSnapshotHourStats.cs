using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReeTrack.Infrastructure.Persistence;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721153000_AddProjectCostSnapshotHourStats")]
public partial class AddProjectCostSnapshotHourStats : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "total_hours",
            table: "project_cost_snapshots",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "weekend_hours",
            table: "project_cost_snapshots",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "holiday_hours",
            table: "project_cost_snapshots",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "overtime_hours",
            table: "project_cost_snapshots",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "total_hours",
            table: "project_cost_snapshots");

        migrationBuilder.DropColumn(
            name: "weekend_hours",
            table: "project_cost_snapshots");

        migrationBuilder.DropColumn(
            name: "holiday_hours",
            table: "project_cost_snapshots");

        migrationBuilder.DropColumn(
            name: "overtime_hours",
            table: "project_cost_snapshots");
    }
}
