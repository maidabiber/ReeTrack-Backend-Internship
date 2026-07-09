using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedTimeEntryApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "status",
                table: "time_entries",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_user_id",
                table: "time_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_time_entries_submitted_by_user_id",
                table: "time_entries",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_user_status",
                table: "time_entries",
                columns: new[] { "user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_time_entries_users_submitted_by_user_id",
                table: "time_entries",
                column: "submitted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_time_entries_users_submitted_by_user_id",
                table: "time_entries");

            migrationBuilder.DropIndex(
                name: "IX_time_entries_submitted_by_user_id",
                table: "time_entries");

            migrationBuilder.DropIndex(
                name: "ix_time_entries_user_status",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "status",
                table: "time_entries");

            migrationBuilder.DropColumn(
                name: "submitted_by_user_id",
                table: "time_entries");
        }
    }
}
