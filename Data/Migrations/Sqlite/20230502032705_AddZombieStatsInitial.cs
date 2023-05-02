using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddZombieStatsInitial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFZombieMatch",
                columns: table => new
                {
                    ZombieMatchId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MapId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServerId = table.Column<long>(type: "INTEGER", nullable: true),
                    MatchStartDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MatchEndDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EFClientClientId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatch", x => x.ZombieMatchId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatch_EFClients_EFClientClientId",
                        column: x => x.EFClientClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieMatch_EFMaps_MapId",
                        column: x => x.MapId,
                        principalTable: "EFMaps",
                        principalColumn: "MapId");
                    table.ForeignKey(
                        name: "FK_EFZombieMatch_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId");
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStat",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageDealt = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageReceived = table.Column<int>(type: "INTEGER", nullable: false),
                    Headshots = table.Column<int>(type: "INTEGER", nullable: false),
                    Melees = table.Column<int>(type: "INTEGER", nullable: false),
                    Downs = table.Column<int>(type: "INTEGER", nullable: false),
                    Revives = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsSpent = table.Column<int>(type: "INTEGER", nullable: false),
                    PerksConsumed = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerupsGrabbed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStat_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStat_EFZombieMatch_MatchId",
                        column: x => x.MatchId,
                        principalTable: "EFZombieMatch",
                        principalColumn: "ZombieMatchId");
                });

            migrationBuilder.CreateTable(
                name: "EFZombieAggregateClientStat",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AverageKillsPerDown = table.Column<double>(type: "REAL", nullable: false),
                    AverageDowns = table.Column<double>(type: "REAL", nullable: false),
                    AverageRevives = table.Column<double>(type: "REAL", nullable: false),
                    HeadshotPercentage = table.Column<double>(type: "REAL", nullable: false),
                    AlivePercentage = table.Column<double>(type: "REAL", nullable: false),
                    AverageMelees = table.Column<double>(type: "REAL", nullable: false),
                    AverageRoundReached = table.Column<double>(type: "REAL", nullable: false),
                    AveragePoints = table.Column<double>(type: "REAL", nullable: false),
                    HighestRound = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRoundsPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMatchesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    RankingMetric = table.Column<double>(type: "REAL", nullable: false),
                    EFClientClientId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieAggregateClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieAggregateClientStat_EFClients_EFClientClientId",
                        column: x => x.EFClientClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieAggregateClientStat_EFZombieClientStat_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatchClientStat",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EFClientClientId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatchClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatchClientStat_EFClients_EFClientClientId",
                        column: x => x.EFClientClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieMatchClientStat_EFZombieClientStat_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieRoundClientStat",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    TimeAlive = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    EFClientClientId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieRoundClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieRoundClientStat_EFClients_EFClientClientId",
                        column: x => x.EFClientClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieRoundClientStat_EFZombieClientStat_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieAggregateClientStat_EFClientClientId",
                table: "EFZombieAggregateClientStat",
                column: "EFClientClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStat_ClientId",
                table: "EFZombieClientStat",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStat_MatchId",
                table: "EFZombieClientStat",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatch_EFClientClientId",
                table: "EFZombieMatch",
                column: "EFClientClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatch_MapId",
                table: "EFZombieMatch",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatch_ServerId",
                table: "EFZombieMatch",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatchClientStat_EFClientClientId",
                table: "EFZombieMatchClientStat",
                column: "EFClientClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieRoundClientStat_EFClientClientId",
                table: "EFZombieRoundClientStat",
                column: "EFClientClientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFZombieAggregateClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieMatchClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieRoundClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieMatch");
        }
    }
}
