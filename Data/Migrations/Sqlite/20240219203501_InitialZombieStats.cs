using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialZombieStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PerformanceBucket",
                table: "EFServers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerformanceBucket",
                table: "EFClientRankingHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFClientStatTags",
                columns: table => new
                {
                    ZombieStatTagId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientStatTags", x => x.ZombieStatTagId);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatches",
                columns: table => new
                {
                    ZombieMatchId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MapId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServerId = table.Column<long>(type: "INTEGER", nullable: true),
                    ClientsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchStartDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MatchEndDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatches", x => x.ZombieMatchId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatches_EFMaps_MapId",
                        column: x => x.MapId,
                        principalTable: "EFMaps",
                        principalColumn: "MapId");
                    table.ForeignKey(
                        name: "FK_EFZombieMatches_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId");
                });

            migrationBuilder.CreateTable(
                name: "EFClientStatTagValues",
                columns: table => new
                {
                    ZombieClientStatTagValueId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatValue = table.Column<int>(type: "INTEGER", nullable: true),
                    StatTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientStatTagValues", x => x.ZombieClientStatTagValueId);
                    table.ForeignKey(
                        name: "FK_EFClientStatTagValues_EFClientStatTags_StatTagId",
                        column: x => x.StatTagId,
                        principalTable: "EFClientStatTags",
                        principalColumn: "ZombieStatTagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientStatTagValues_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStats",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageDealt = table.Column<long>(type: "INTEGER", nullable: false),
                    DamageReceived = table.Column<int>(type: "INTEGER", nullable: false),
                    Headshots = table.Column<int>(type: "INTEGER", nullable: false),
                    HeadshotKills = table.Column<int>(type: "INTEGER", nullable: false),
                    Melees = table.Column<int>(type: "INTEGER", nullable: false),
                    Downs = table.Column<int>(type: "INTEGER", nullable: false),
                    Revives = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsEarned = table.Column<long>(type: "INTEGER", nullable: false),
                    PointsSpent = table.Column<long>(type: "INTEGER", nullable: false),
                    PerksConsumed = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerupsGrabbed = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieClientStats", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStats_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStats_EFZombieMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "EFZombieMatches",
                        principalColumn: "ZombieMatchId");
                });

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
                        name: "FK_EFZombieEvents_EFZombieMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "EFZombieMatches",
                        principalColumn: "ZombieMatchId");
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStatAggregates",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<long>(type: "INTEGER", nullable: true),
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
                    TotalMatchesCompleted = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieClientStatAggregates", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatAggregates_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId");
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatAggregates_EFZombieClientStats_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatchClientStats",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatchClientStats", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatchClientStats_EFZombieClientStats_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieRoundClientStats",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    TimeAlive = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieRoundClientStats", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieRoundClientStats_EFZombieClientStats_ZombieClientStatId",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStatRecords",
                columns: table => new
                {
                    ZombieClientStatRecordId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    RoundId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieClientStatRecords", x => x.ZombieClientStatRecordId);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatRecords_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatRecords_EFZombieRoundClientStats_RoundId",
                        column: x => x.RoundId,
                        principalTable: "EFZombieRoundClientStats",
                        principalColumn: "ZombieClientStatId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatTagValues_ClientId",
                table: "EFClientStatTagValues",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatTagValues_StatTagId",
                table: "EFClientStatTagValues",
                column: "StatTagId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatAggregates_ServerId",
                table: "EFZombieClientStatAggregates",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatRecords_ClientId",
                table: "EFZombieClientStatRecords",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatRecords_RoundId",
                table: "EFZombieClientStatRecords",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStats_ClientId",
                table: "EFZombieClientStats",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStats_MatchId",
                table: "EFZombieClientStats",
                column: "MatchId");

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

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_MapId",
                table: "EFZombieMatches",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_ServerId",
                table: "EFZombieMatches",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFClientStatTagValues");

            migrationBuilder.DropTable(
                name: "EFZombieClientStatAggregates");

            migrationBuilder.DropTable(
                name: "EFZombieClientStatRecords");

            migrationBuilder.DropTable(
                name: "EFZombieEvents");

            migrationBuilder.DropTable(
                name: "EFZombieMatchClientStats");

            migrationBuilder.DropTable(
                name: "EFClientStatTags");

            migrationBuilder.DropTable(
                name: "EFZombieRoundClientStats");

            migrationBuilder.DropTable(
                name: "EFZombieClientStats");

            migrationBuilder.DropTable(
                name: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "PerformanceBucket",
                table: "EFServers");

            migrationBuilder.DropColumn(
                name: "PerformanceBucket",
                table: "EFClientRankingHistory");
        }
    }
}
