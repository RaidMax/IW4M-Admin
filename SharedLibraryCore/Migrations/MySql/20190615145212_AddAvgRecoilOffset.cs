using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.MySql
{
    public partial class AddAvgRecoilOffset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRecoilOffset",
                table: "EFClientStatistics",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
