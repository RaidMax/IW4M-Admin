using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class AddIsPasswordProtectedColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.AddColumn<bool>(
                   name: "IsPasswordProtected",
                   type: "bool",
                   table: "EFServers",
                   nullable: false,
                   defaultValue: false);
            }
            else
            {
                migrationBuilder.AddColumn<bool>(
                    name: "IsPasswordProtected",
                    table: "EFServers",
                    nullable: false,
                    defaultValue: false);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPasswordProtected",
                table: "EFServers");
        }
    }
}
