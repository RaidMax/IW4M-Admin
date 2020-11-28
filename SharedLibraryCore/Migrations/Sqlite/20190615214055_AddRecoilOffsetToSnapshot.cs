using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class AddRecoilOffsetToSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "RecoilOffset",
                table: "EFACSnapshot",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoilOffset",
                table: "EFACSnapshot");
        }
    }
}
