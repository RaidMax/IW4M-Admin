using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
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
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDateTime",
                table: "EFPenaltyIdentifiers",
                type: "timestamp without time zone",
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
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
