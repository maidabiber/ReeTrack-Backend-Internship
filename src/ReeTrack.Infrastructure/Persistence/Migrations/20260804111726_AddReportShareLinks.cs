using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    report_type = table.Column<short>(type: "smallint", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    query_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    spec_json = table.Column<string>(type: "text", nullable: true),
                    access_level = table.Column<short>(type: "smallint", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_share_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_share_links_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_share_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    share_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_share_recipients", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_share_recipients_report_share_links_share_link_id",
                        column: x => x.share_link_id,
                        principalTable: "report_share_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_report_share_recipients_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_report_share_links_report_type",
                table: "report_share_links",
                column: "report_type");

            migrationBuilder.CreateIndex(
                name: "ux_report_share_links_token",
                table: "report_share_links",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_report_share_recipients_link_user",
                table: "report_share_recipients",
                columns: new[] { "share_link_id", "user_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_share_recipients");

            migrationBuilder.DropTable(
                name: "report_share_links");
        }
    }
}
