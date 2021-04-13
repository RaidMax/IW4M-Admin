using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class AddRankingHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFClientRankingHistory",
                columns: table => new
                {
                    ClientRankingHistoryId = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedDateTime = table.Column<DateTime>(nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(nullable: true),
                    ClientId = table.Column<int>(nullable: false),
                    ServerId = table.Column<long>(nullable: true),
                    Newest = table.Column<bool>(nullable: false),
                    Ranking = table.Column<int>(nullable: true),
                    ZScore = table.Column<double>(nullable: true),
                    PerformanceMetric = table.Column<double>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientRankingHistory", x => x.ClientRankingHistoryId);
                    table.ForeignKey(
                        name: "FK_EFClientRankingHistory_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientRankingHistory_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_ClientId",
                table: "EFClientRankingHistory",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_Ranking",
                table: "EFClientRankingHistory",
                column: "Ranking");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_ServerId",
                table: "EFClientRankingHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_UpdatedDateTime",
                table: "EFClientRankingHistory",
                column: "UpdatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_ZScore",
                table: "EFClientRankingHistory",
                column: "ZScore");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFClientRankingHistory");
        }
    }
}
