using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddAlternateKeyToEFClients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFClients_NetworkId",
                table: "EFClients");

            migrationBuilder.AlterColumn<int>(
                name: "GameName",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EFClients_NetworkId_GameName",
                table: "EFClients",
                columns: new[] { "NetworkId", "GameName" });

            migrationBuilder.CreateIndex(
                name: "IX_EFClients_NetworkId",
                table: "EFClients",
                column: "NetworkId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_EFClients_NetworkId_GameName",
                table: "EFClients");

            migrationBuilder.DropIndex(
                name: "IX_EFClients_NetworkId",
                table: "EFClients");

            migrationBuilder.AlterColumn<int>(
                name: "GameName",
                table: "EFClients",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_EFClients_NetworkId",
                table: "EFClients",
                column: "NetworkId",
                unique: true);
        }
    }
}
