using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReeTrack.Infrastructure.Persistence;

#nullable disable

namespace ReeTrack.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260723100000_AddTimeEntryTemplateTags")]
public partial class AddTimeEntryTemplateTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "time_entry_template_tags",
            columns: table => new
            {
                time_entry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_time_entry_template_tags", x => new { x.time_entry_template_id, x.tag_id });
                table.ForeignKey(
                    name: "FK_time_entry_template_tags_tags_tag_id",
                    column: x => x.tag_id,
                    principalTable: "tags",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_time_entry_template_tags_time_entry_templates_time_entry_tem~",
                    column: x => x.time_entry_template_id,
                    principalTable: "time_entry_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_time_entry_template_tags_tag_id",
            table: "time_entry_template_tags",
            column: "tag_id");

        migrationBuilder.Sql(
            """
            INSERT INTO time_entry_template_tags (time_entry_template_id, tag_id)
            SELECT t.id, tet.tag_id
            FROM time_entry_templates t
            INNER JOIN time_entry_tags tet ON tet.time_entry_id = t.time_entry_id
            ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "time_entry_template_tags");
    }
}
