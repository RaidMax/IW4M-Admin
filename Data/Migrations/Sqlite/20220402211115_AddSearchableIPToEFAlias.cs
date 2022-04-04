using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddSearchableIPToEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchableIPAddress",
                table: "EFAlias",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchableIPAddress",
                table: "EFAlias");
        }
    }
}
