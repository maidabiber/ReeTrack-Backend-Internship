using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHourlyRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_hourly_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_hourly_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_hourly_rates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_hourly_rates_user_id_valid_from",
                table: "user_hourly_rates",
                columns: new[] { "user_id", "valid_from" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO user_hourly_rates (id, user_id, hourly_rate, currency_code, valid_from, valid_to, created_at_utc, updated_at_utc)
                SELECT gen_random_uuid(),
                       u.id,
                       12.82,
                       'EUR',
                       (u.created_at_utc AT TIME ZONE 'UTC')::date,
                       NULL,
                       NOW() AT TIME ZONE 'UTC',
                       NOW() AT TIME ZONE 'UTC'
                FROM users u
                WHERE NOT EXISTS (
                    SELECT 1 FROM user_hourly_rates r WHERE r.user_id = u.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_hourly_rates");
        }
    }
}
