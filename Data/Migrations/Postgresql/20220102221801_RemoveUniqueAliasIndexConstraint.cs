using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Postgresql
{
    public partial class RemoveUniqueAliasIndexConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_Name_IPAddress",
                table: "EFAlias");

            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_Name_IPAddress",
                table: "EFAlias",
                columns: new[] { "Name", "IPAddress" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_Name_IPAddress",
                table: "EFAlias");

            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_Name_IPAddress",
                table: "EFAlias",
                columns: new[] { "Name", "IPAddress" },
                unique: true);
        }
    }
}
