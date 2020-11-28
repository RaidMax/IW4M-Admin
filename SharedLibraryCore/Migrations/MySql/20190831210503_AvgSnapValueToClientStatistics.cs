using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.MySql
{
    public partial class AvgSnapValueToClientStatistics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageSnapValue",
                table: "EFClientStatistics",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageSnapValue",
                table: "EFClientStatistics");
        }
    }
}
