using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    public partial class AddSearchableIPToEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchableIPAddress",
                table: "EFAlias",
                type: "longtext",
                nullable: true,
                computedColumnSql: "CONCAT((IPAddress & 255), \".\", ((IPAddress >> 8) & 255), \".\", ((IPAddress >> 16) & 255), \".\", ((IPAddress >> 24) & 255))",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchableIPAddress",
                table: "EFAlias");
        }
    }
}
