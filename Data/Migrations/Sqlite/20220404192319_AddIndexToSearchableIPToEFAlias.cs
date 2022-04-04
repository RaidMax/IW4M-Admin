using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddIndexToSearchableIPToEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_SearchableIPAddress",
                table: "EFAlias",
                column: "SearchableIPAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_SearchableIPAddress",
                table: "EFAlias");
        }
    }
}
