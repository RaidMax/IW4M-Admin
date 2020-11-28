using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Postgresql
{
    public partial class AddEFAliasNameIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_Name",
                table: "EFAlias",
                column: "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_Name",
                table: "EFAlias");
        }
    }
}
