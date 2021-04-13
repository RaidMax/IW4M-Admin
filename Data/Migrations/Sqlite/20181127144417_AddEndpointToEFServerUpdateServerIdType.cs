using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class AddEndpointToEFServerUpdateServerIdType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndPoint",
                table: "EFServers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndPoint",
                table: "EFServers");
        }
    }
}
