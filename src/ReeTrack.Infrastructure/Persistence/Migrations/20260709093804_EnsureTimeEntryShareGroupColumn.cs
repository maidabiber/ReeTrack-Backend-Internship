using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureTimeEntryShareGroupColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE time_entries
                ADD COLUMN IF NOT EXISTS share_group_id uuid;

                CREATE INDEX IF NOT EXISTS ix_time_entries_share_group_id
                ON time_entries (share_group_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_time_entries_share_group_id;

                ALTER TABLE time_entries
                DROP COLUMN IF EXISTS share_group_id;
                """);
        }
    }
}
