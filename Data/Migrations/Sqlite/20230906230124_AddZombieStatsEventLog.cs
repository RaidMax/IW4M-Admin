using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddZombieStatsEventLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFZombieMatch_EFClients_EFClientClientId",
                table: "EFZombieMatch");

            migrationBuilder.DropIndex(
                name: "IX_EFZombieMatch_EFClientClientId",
                table: "EFZombieMatch");

            migrationBuilder.DropColumn(
                name: "EFClientClientId",
                table: "EFZombieMatch");

            migrationBuilder.CreateTable(
                name: "EFZombieEvents",
                columns: table => new
                {
                    ZombieEventLogId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssociatedClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    NumericalValue = table.Column<double>(type: "REAL", nullable: true),
                    TextualValue = table.Column<string>(type: "TEXT", nullable: true),
                    MatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieEvents", x => x.ZombieEventLogId);
                    table.ForeignKey(
                        name: "FK_EFZombieEvents_EFClients_AssociatedClientId",
                        column: x => x.AssociatedClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieEvents_EFClients_SourceClientId",
                        column: x => x.SourceClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieEvents_EFZombieMatch_MatchId",
                        column: x => x.MatchId,
                        principalTable: "EFZombieMatch",
                        principalColumn: "ZombieMatchId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_AssociatedClientId",
                table: "EFZombieEvents",
                column: "AssociatedClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_MatchId",
                table: "EFZombieEvents",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieEvents_SourceClientId",
                table: "EFZombieEvents",
                column: "SourceClientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFZombieEvents");

            migrationBuilder.AddColumn<int>(
                name: "EFClientClientId",
                table: "EFZombieMatch",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatch_EFClientClientId",
                table: "EFZombieMatch",
                column: "EFClientClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFZombieMatch_EFClients_EFClientClientId",
                table: "EFZombieMatch",
                column: "EFClientClientId",
                principalTable: "EFClients",
                principalColumn: "ClientId");
        }
    }
}
