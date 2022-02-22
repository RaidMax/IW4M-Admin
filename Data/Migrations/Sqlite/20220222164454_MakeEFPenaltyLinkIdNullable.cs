using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
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
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
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
