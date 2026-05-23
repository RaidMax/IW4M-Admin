using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class ExpandZombieAggregatesAndDropClientsCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieEvents_MatchId",
                table: "EFZombieEvents");

            migrationBuilder.DropIndex(
                name: "IX_EFZombieEvents_SourceClientId",
                table: "EFZombieEvents");

            migrationBuilder.DropColumn(
                name: "ClientsCompleted",
                table: "EFZombieMatches");

            migrationBuilder.AddColumn<int>(
                name: "BankOperations",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GumsActivated",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GumsTaken",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LockerOperations",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponsAbandoned",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_MatchId_EventType",
                table: "EFZombieEvents",
                columns: new[] { "MatchId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_SourceClientId_EventType",
                table: "EFZombieEvents",
                columns: new[] { "SourceClientId", "EventType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieEvents_MatchId_EventType",
                table: "EFZombieEvents");

            migrationBuilder.DropIndex(
                name: "IX_EFZombieEvents_SourceClientId_EventType",
                table: "EFZombieEvents");

            migrationBuilder.DropColumn(
                name: "BankOperations",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "GumsActivated",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "GumsTaken",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "LockerOperations",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "WeaponsAbandoned",
                table: "EFZombieClientStats");

            migrationBuilder.AddColumn<int>(
                name: "ClientsCompleted",
                table: "EFZombieMatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_MatchId",
                table: "EFZombieEvents",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_SourceClientId",
                table: "EFZombieEvents",
                column: "SourceClientId");
        }
    }
}
