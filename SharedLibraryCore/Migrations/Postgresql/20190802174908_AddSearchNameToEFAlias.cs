using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Postgresql
{
    public partial class AddSearchNameToEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchableName",
                table: "EFAlias",
                maxLength: 24,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_SearchableName",
                table: "EFAlias",
                column: "SearchableName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_SearchableName",
                table: "EFAlias");

            migrationBuilder.DropColumn(
                name: "SearchableName",
                table: "EFAlias");
        }
    }
}
