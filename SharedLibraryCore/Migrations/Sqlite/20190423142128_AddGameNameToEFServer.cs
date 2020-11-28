using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class AddGameNameToEFServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameName",
                table: "EFServers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameName",
                table: "EFServers");
        }
    }
}
