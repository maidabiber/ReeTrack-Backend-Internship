using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReeTrack.Infrastructure.Persistence;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260721161500_AddRateMultiplierSettings")]
public partial class AddRateMultiplierSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rate_multiplier_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                weekend_premium = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                holiday_premium = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                overtime_premium = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                weekly_overtime_threshold_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rate_multiplier_settings", x => x.id);
            });

        // InsertData needs a mapped entity in the migration model; this hand-written
        // migration has no Designer target model, so seed via SQL instead.
        migrationBuilder.Sql(
            """
            INSERT INTO rate_multiplier_settings (
                id,
                weekend_premium,
                holiday_premium,
                overtime_premium,
                weekly_overtime_threshold_hours,
                created_at_utc,
                updated_at_utc)
            VALUES (
                'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
                0.5,
                1.0,
                0.5,
                40,
                TIMESTAMPTZ '2026-01-01 00:00:00+00',
                TIMESTAMPTZ '2026-01-01 00:00:00+00');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rate_multiplier_settings");
    }
}
