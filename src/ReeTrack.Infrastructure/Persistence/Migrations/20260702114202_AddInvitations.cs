using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role_id = table.Column<short>(type: "smallint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitations_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invitations_users_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invitations_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_accepted_by_user_id",
                table: "invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_email_pending",
                table: "invitations",
                column: "email",
                unique: true,
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_invited_by_user_id",
                table: "invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_role_id",
                table: "invitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                table: "invitations",
                column: "token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations");
        }
    }
}
