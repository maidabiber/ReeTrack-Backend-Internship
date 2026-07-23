using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReeTrack.Infrastructure.Persistence;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722124000_ExpandHolidaysAndCalendarSettings")]
public partial class ExpandHolidaysAndCalendarSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "name",
            table: "holidays",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "Holiday");

        migrationBuilder.AddColumn<bool>(
            name: "is_active",
            table: "holidays",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<short>(
            name: "source",
            table: "holidays",
            type: "smallint",
            nullable: false,
            defaultValue: (short)1);

        migrationBuilder.AddColumn<string>(
            name: "country_code",
            table: "holidays",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "holiday_calendar_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_holiday_calendar_settings", x => x.id);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO holiday_calendar_settings (
                id,
                country_code,
                created_at_utc,
                updated_at_utc)
            VALUES (
                'bbbbbbbb-cccc-dddd-eeee-ffffffffffff',
                NULL,
                TIMESTAMPTZ '2026-01-01 00:00:00+00',
                TIMESTAMPTZ '2026-01-01 00:00:00+00');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "holiday_calendar_settings");

        migrationBuilder.DropColumn(name: "country_code", table: "holidays");
        migrationBuilder.DropColumn(name: "source", table: "holidays");
        migrationBuilder.DropColumn(name: "is_active", table: "holidays");
        migrationBuilder.DropColumn(name: "name", table: "holidays");
    }
}
