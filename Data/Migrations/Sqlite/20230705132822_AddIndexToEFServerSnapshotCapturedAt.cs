using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddIndexToEFServerSnapshotCapturedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFServerSnapshot_CapturedAt",
                table: "EFServerSnapshot",
                column: "CapturedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFServerSnapshot_CapturedAt",
                table: "EFServerSnapshot");
        }
    }
}
