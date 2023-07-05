using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    public partial class AddIndexToEFServerSnapshotCapturedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SearchableIPAddress",
                table: "EFAlias",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                computedColumnSql: "((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)",
                oldStored: true);

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

            migrationBuilder.AlterColumn<string>(
                name: "SearchableIPAddress",
                table: "EFAlias",
                type: "text",
                nullable: true,
                computedColumnSql: "((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)",
                stored: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true,
                oldComputedColumnSql: "((IPAddress & 255) || '.' || ((IPAddress >> 8) & 255)) || '.' || ((IPAddress >> 16) & 255) || '.' || ((IPAddress >> 24) & 255)",
                oldStored: true);
        }
    }
}
