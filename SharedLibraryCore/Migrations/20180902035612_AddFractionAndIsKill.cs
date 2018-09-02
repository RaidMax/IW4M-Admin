using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class AddFractionAndIsKill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Fraction",
                table: "EFClientKills",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsKill",
                table: "EFClientKills",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EFChangeHistory",
                columns: table => new
                {
                    Active = table.Column<bool>(nullable: false),
                    ChangeHistoryId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginEntityId = table.Column<int>(nullable: false),
                    TargetEntityId = table.Column<int>(nullable: false),
                    TypeOfChange = table.Column<int>(nullable: false),
                    TimeChanged = table.Column<DateTime>(nullable: false),
                    Comment = table.Column<string>(maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFChangeHistory", x => x.ChangeHistoryId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFChangeHistory");

            migrationBuilder.DropIndex(
                name: "IX_Vector3_EFACSnapshotSnapshotId",
                table: "Vector3");

            migrationBuilder.DropColumn(
                name: "Fraction",
                table: "EFClientKills");

            migrationBuilder.DropColumn(
                name: "IsKill",
                table: "EFClientKills");
        }
    }
}
