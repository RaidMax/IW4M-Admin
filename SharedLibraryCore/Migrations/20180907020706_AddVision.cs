using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class AddVision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "VisionAverage",
                table: "EFClientStatistics",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisionAverage",
                table: "EFClientStatistics");
        }
    }
}
