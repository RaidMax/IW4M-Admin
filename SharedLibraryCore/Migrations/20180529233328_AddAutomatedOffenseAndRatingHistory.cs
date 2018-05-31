using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations
{
    public partial class AddAutomatedOffenseAndRatingHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomatedOffense",
                table: "EFPenalties",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFClientAverageStatHistory",
                columns: table => new
                {
                    ClientId = table.Column<int>(nullable: false),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientAverageStatHistory", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_EFClientAverageStatHistory_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFClientStatHistory",
                columns: table => new
                {
                    StatHistoryId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    ClientId = table.Column<int>(nullable: false),
                    ServerId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientStatHistory", x => x.StatHistoryId);
                    table.ForeignKey(
                        name: "FK_EFClientStatHistory_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientStatHistory_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFRating",
                columns: table => new
                {
                    RatingId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    ClientId = table.Column<int>(nullable: false),
                    EFClientAverageStatHistoryClientId = table.Column<int>(nullable: true),
                    EFClientStatHistoryStatHistoryId = table.Column<int>(nullable: true),
                    Performance = table.Column<double>(nullable: false),
                    Ranking = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFRating", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_EFRating_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFRating_EFClientAverageStatHistory_EFClientAverageStatHistoryClientId",
                        column: x => x.EFClientAverageStatHistoryClientId,
                        principalTable: "EFClientAverageStatHistory",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFRating_EFClientStatHistory_EFClientStatHistoryStatHistoryId",
                        column: x => x.EFClientStatHistoryStatHistoryId,
                        principalTable: "EFClientStatHistory",
                        principalColumn: "StatHistoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatHistory_ClientId",
                table: "EFClientStatHistory",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatHistory_ServerId",
                table: "EFClientStatHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_ClientId",
                table: "EFRating",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_EFClientAverageStatHistoryClientId",
                table: "EFRating",
                column: "EFClientAverageStatHistoryClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_EFClientStatHistoryStatHistoryId",
                table: "EFRating",
                column: "EFClientStatHistoryStatHistoryId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFRating");

            migrationBuilder.DropTable(
                name: "EFClientAverageStatHistory");

            migrationBuilder.DropTable(
                name: "EFClientStatHistory");

            migrationBuilder.DropColumn(
                name: "AutomatedOffense",
                table: "EFPenalties");
        }
    }
}
