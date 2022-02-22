using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    public partial class MakeEFPenaltyLinkIdNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFPenalties_EFAliasLinks_LinkId",
                table: "EFPenalties");

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFPenalties",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_EFPenalties_EFAliasLinks_LinkId",
                table: "EFPenalties",
                column: "LinkId",
                principalTable: "EFAliasLinks",
                principalColumn: "AliasLinkId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFPenalties_EFAliasLinks_LinkId",
                table: "EFPenalties");

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFPenalties",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EFPenalties_EFAliasLinks_LinkId",
                table: "EFPenalties",
                column: "LinkId",
                principalTable: "EFAliasLinks",
                principalColumn: "AliasLinkId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
