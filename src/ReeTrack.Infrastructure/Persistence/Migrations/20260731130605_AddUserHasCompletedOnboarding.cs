using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHasCompletedOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_completed_onboarding",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Existing Active/Disabled users skip the first-track tour.
            // Pending Invited users stay false so they see the tour after activate.
            migrationBuilder.Sql(
                """
                UPDATE users
                SET has_completed_onboarding = true
                WHERE status <> 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_completed_onboarding",
                table: "users");
        }
    }
}
