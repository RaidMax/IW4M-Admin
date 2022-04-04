using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    public partial class AddSearchableIPToEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchableIPAddress",
                table: "EFAlias",
                type: "text",
                nullable: true,
                computedColumnSql: "CASE WHEN \"IPAddress\" IS NULL THEN 'NULL'::text ELSE (\"IPAddress\" & 255)::text || '.'::text || ((\"IPAddress\" >> 8) & 255)::text || '.'::text || ((\"IPAddress\" >> 16) & 255)::text || '.'::text || ((\"IPAddress\" >> 24) & 255)::text END",
                stored: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchableIPAddress",
                table: "EFAlias");
        }
    }
}
