using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddWeaponReferenceAndServerIdToEFACSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ServerId",
                table: "EFACSnapshot",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeaponReference",
                table: "EFACSnapshot",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_ServerId",
                table: "EFACSnapshot",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFACSnapshot_EFServers_ServerId",
                table: "EFACSnapshot",
                column: "ServerId",
                principalTable: "EFServers",
                principalColumn: "ServerId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFACSnapshot_EFServers_ServerId",
                table: "EFACSnapshot");

            migrationBuilder.DropIndex(
                name: "IX_EFACSnapshot_ServerId",
                table: "EFACSnapshot");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "EFACSnapshot");

            migrationBuilder.DropColumn(
                name: "WeaponReference",
                table: "EFACSnapshot");
        }
    }
}
