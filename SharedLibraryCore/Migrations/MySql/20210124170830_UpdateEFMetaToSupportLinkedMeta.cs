using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.MySql
{
    public partial class UpdateEFMetaToSupportLinkedMeta : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedMetaId",
                table: "EFMeta",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFMeta_LinkedMetaId",
                table: "EFMeta",
                column: "LinkedMetaId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFMeta_EFMeta_LinkedMetaId",
                table: "EFMeta",
                column: "LinkedMetaId",
                principalTable: "EFMeta",
                principalColumn: "MetaId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFMeta_EFMeta_LinkedMetaId",
                table: "EFMeta");

            migrationBuilder.DropIndex(
                name: "IX_EFMeta_LinkedMetaId",
                table: "EFMeta");

            migrationBuilder.DropColumn(
                name: "LinkedMetaId",
                table: "EFMeta");
        }
    }
}
