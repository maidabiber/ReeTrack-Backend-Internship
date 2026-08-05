using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_thresholds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_type = table.Column<short>(type: "smallint", nullable: false),
                    threshold_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_triggered = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_thresholds", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_thresholds_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pending_project_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    threshold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_type = table.Column<short>(type: "smallint", nullable: false),
                    project_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    threshold_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    cost_percentage = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    calculated_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fixed_fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    hours_percentage = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    actual_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    time_estimate_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    deliver_after_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_project_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_project_alerts_project_thresholds_threshold_id",
                        column: x => x.threshold_id,
                        principalTable: "project_thresholds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pending_project_alerts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_project_alerts_delivered_deliver_after",
                table: "pending_project_alerts",
                columns: new[] { "delivered_at_utc", "deliver_after_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_project_alerts_project_id",
                table: "pending_project_alerts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_project_alerts_threshold_id",
                table: "pending_project_alerts",
                column: "threshold_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_thresholds_project_id_metric_type_threshold_percentage",
                table: "project_thresholds",
                columns: new[] { "project_id", "metric_type", "threshold_percentage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_project_alerts");

            migrationBuilder.DropTable(
                name: "project_thresholds");
        }
    }
}
