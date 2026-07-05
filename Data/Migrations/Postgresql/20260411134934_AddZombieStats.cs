using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddZombieStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformanceBucketId",
                table: "EFServers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformanceBucketId",
                table: "EFClientRankingHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformanceBucketId",
                table: "EFClientHitStatistics",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFClientStatTags",
                columns: table => new
                {
                    ZombieStatTagId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TagName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientStatTags", x => x.ZombieStatTagId);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatches",
                columns: table => new
                {
                    ZombieMatchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MapId = table.Column<int>(type: "integer", nullable: true),
                    ServerId = table.Column<long>(type: "bigint", nullable: true),
                    ClientsCompleted = table.Column<int>(type: "integer", nullable: false),
                    PlayerCount = table.Column<int>(type: "integer", nullable: true),
                    HighestRound = table.Column<int>(type: "integer", nullable: false),
                    MatchStartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MatchEndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "PerformanceBuckets",
                columns: table => new
                {
                    PerformanceBucketId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceBuckets", x => x.PerformanceBucketId);
                });

            migrationBuilder.CreateTable(
                name: "EFClientStatTagValues",
                columns: table => new
                {
                    ZombieClientStatTagValueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatValue = table.Column<int>(type: "integer", nullable: true),
                    StatTagId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    ZombieClientStatId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<int>(type: "integer", nullable: true),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    DamageDealt = table.Column<long>(type: "bigint", nullable: false),
                    DamageReceived = table.Column<int>(type: "integer", nullable: false),
                    Headshots = table.Column<int>(type: "integer", nullable: false),
                    HeadshotKills = table.Column<int>(type: "integer", nullable: false),
                    Melees = table.Column<int>(type: "integer", nullable: false),
                    Downs = table.Column<int>(type: "integer", nullable: false),
                    Revives = table.Column<int>(type: "integer", nullable: false),
                    PointsEarned = table.Column<long>(type: "bigint", nullable: false),
                    PointsSpent = table.Column<long>(type: "bigint", nullable: false),
                    PerksConsumed = table.Column<int>(type: "integer", nullable: false),
                    PowerupsGrabbed = table.Column<int>(type: "integer", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    ZombieEventLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    SourceClientId = table.Column<int>(type: "integer", nullable: true),
                    AssociatedClientId = table.Column<int>(type: "integer", nullable: true),
                    NumericalValue = table.Column<double>(type: "double precision", nullable: true),
                    TextualValue = table.Column<string>(type: "text", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    ZombieClientStatId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: true),
                    AverageKillsPerDown = table.Column<double>(type: "double precision", nullable: false),
                    AverageDowns = table.Column<double>(type: "double precision", nullable: false),
                    AverageRevives = table.Column<double>(type: "double precision", nullable: false),
                    HeadshotPercentage = table.Column<double>(type: "double precision", nullable: false),
                    AlivePercentage = table.Column<double>(type: "double precision", nullable: false),
                    AverageMelees = table.Column<double>(type: "double precision", nullable: false),
                    AverageRoundReached = table.Column<double>(type: "double precision", nullable: false),
                    AveragePoints = table.Column<double>(type: "double precision", nullable: false),
                    HighestRound = table.Column<int>(type: "integer", nullable: false),
                    TotalRoundsPlayed = table.Column<int>(type: "integer", nullable: false),
                    TotalMatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    TotalMatchesCompleted = table.Column<int>(type: "integer", nullable: false)
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
                        name: "FK_EFZombieClientStatAggregates_EFZombieClientStats_ZombieClie~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatchClientStats",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatchClientStats", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatchClientStats_EFZombieClientStats_ZombieClientSt~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieRoundClientStats",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "bigint", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TimeAlive = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieRoundClientStats", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieRoundClientStats_EFZombieClientStats_ZombieClientSt~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStats",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStatRecords",
                columns: table => new
                {
                    ZombieClientStatRecordId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: true),
                    RoundId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_EFServers_PerformanceBucketId",
                table: "EFServers",
                column: "PerformanceBucketId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_PerformanceBucketId",
                table: "EFClientRankingHistory",
                column: "PerformanceBucketId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientHitStatistics_PerformanceBucketId",
                table: "EFClientHitStatistics",
                column: "PerformanceBucketId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientHitStatistics_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientHitStatistics",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientRankingHistory_PerformanceBuckets_PerformanceBucket~",
                table: "EFClientRankingHistory",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFServers_PerformanceBuckets_PerformanceBucketId",
                table: "EFServers",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFClientHitStatistics_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientHitStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_EFClientRankingHistory_PerformanceBuckets_PerformanceBucket~",
                table: "EFClientRankingHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_EFServers_PerformanceBuckets_PerformanceBucketId",
                table: "EFServers");

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
                name: "PerformanceBuckets");

            migrationBuilder.DropTable(
                name: "EFClientStatTags");

            migrationBuilder.DropTable(
                name: "EFZombieRoundClientStats");

            migrationBuilder.DropTable(
                name: "EFZombieClientStats");

            migrationBuilder.DropTable(
                name: "EFZombieMatches");

            migrationBuilder.DropIndex(
                name: "IX_EFServers_PerformanceBucketId",
                table: "EFServers");

            migrationBuilder.DropIndex(
                name: "IX_EFClientRankingHistory_PerformanceBucketId",
                table: "EFClientRankingHistory");

            migrationBuilder.DropIndex(
                name: "IX_EFClientHitStatistics_PerformanceBucketId",
                table: "EFClientHitStatistics");

            migrationBuilder.DropColumn(
                name: "PerformanceBucketId",
                table: "EFServers");

            migrationBuilder.DropColumn(
                name: "PerformanceBucketId",
                table: "EFClientRankingHistory");

            migrationBuilder.DropColumn(
                name: "PerformanceBucketId",
                table: "EFClientHitStatistics");
        }
    }
}
