using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class AddPreviousCurrentValueToEFChangeHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentValue",
                table: "EFChangeHistory",
                nullable: true);

            migrationBuilder.AddColumn<string>(

                name: "PreviousValue",
                table: "EFChangeHistory",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentValue",
                table: "EFChangeHistory");

            migrationBuilder.DropColumn(
                name: "PreviousValue",
                table: "EFChangeHistory");
        }
    }
}
