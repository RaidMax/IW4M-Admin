using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Postgresql
{
    public partial class UpdateEFMetaToSupportNonClientMeta : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFMeta_EFClients_ClientId",
                table: "EFMeta");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFMeta",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_EFMeta_EFClients_ClientId",
                table: "EFMeta",
                column: "ClientId",
                principalTable: "EFClients",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFMeta_EFClients_ClientId",
                table: "EFMeta");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFMeta",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EFMeta_EFClients_ClientId",
                table: "EFMeta",
                column: "ClientId",
                principalTable: "EFClients",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
