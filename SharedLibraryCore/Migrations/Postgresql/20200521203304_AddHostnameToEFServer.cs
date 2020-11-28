using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Postgresql
{
    public partial class AddHostnameToEFServer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostName",
                table: "EFServers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostName",
                table: "EFServers");
        }
    }
}
