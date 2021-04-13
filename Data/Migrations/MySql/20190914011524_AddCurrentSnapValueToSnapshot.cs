using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddCurrentSnapValueToSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SessionAverageSnapValue",
                table: "EFACSnapshot",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionAverageSnapValue",
                table: "EFACSnapshot");
        }
    }
}
