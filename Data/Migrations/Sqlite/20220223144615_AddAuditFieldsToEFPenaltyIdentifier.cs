using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddAuditFieldsToEFPenaltyIdentifier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "EFPenaltyIdentifiers");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateTime",
                table: "EFPenaltyIdentifiers",
                type: "TEXT",
                nullable: false,
                defaultValue: DateTime.UtcNow);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDateTime",
                table: "EFPenaltyIdentifiers",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDateTime",
                table: "EFPenaltyIdentifiers");

            migrationBuilder.DropColumn(
                name: "UpdatedDateTime",
                table: "EFPenaltyIdentifiers");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "EFPenaltyIdentifiers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
