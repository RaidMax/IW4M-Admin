using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations.Postgresql
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
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerformanceBucket",
                table: "EFClientRankingHistory",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFZombieMatch",
                columns: table => new
                {
                    ZombieMatchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MapId = table.Column<int>(type: "integer", nullable: true),
                    ServerId = table.Column<long>(type: "bigint", nullable: true),
                    ClientsCompleted = table.Column<int>(type: "integer", nullable: false),
                    MatchStartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MatchEndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatch", x => x.ZombieMatchId);
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
                        name: "FK_EFZombieEvents_EFZombieMatch_MatchId",
                        column: x => x.MatchId,
                        principalTable: "EFZombieMatch",
                        principalColumn: "ZombieMatchId");
                });

            migrationBuilder.CreateTable(
                name: "EFZombieAggregateClientStat",
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
                    table.PrimaryKey("PK_EFZombieAggregateClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieAggregateClientStat_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId");
                    table.ForeignKey(
                        name: "FK_EFZombieAggregateClientStat_EFZombieClientStat_ZombieClient~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieMatchClientStat",
                columns: table => new
                {
                    ZombieClientStatId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieMatchClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieMatchClientStat_EFZombieClientStat_ZombieClientStat~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieRoundClientStat",
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
                    table.PrimaryKey("PK_EFZombieRoundClientStat", x => x.ZombieClientStatId);
                    table.ForeignKey(
                        name: "FK_EFZombieRoundClientStat_EFZombieClientStat_ZombieClientStat~",
                        column: x => x.ZombieClientStatId,
                        principalTable: "EFZombieClientStat",
                        principalColumn: "ZombieClientStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFZombieClientStatRecord",
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
                    table.PrimaryKey("PK_EFZombieClientStatRecord", x => x.ZombieClientStatRecordId);
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatRecord_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId");
                    table.ForeignKey(
                        name: "FK_EFZombieClientStatRecord_EFZombieRoundClientStat_RoundId",
                        column: x => x.RoundId,
                        principalTable: "EFZombieRoundClientStat",
                        principalColumn: "ZombieClientStatId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieAggregateClientStat_ServerId",
                table: "EFZombieAggregateClientStat",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStat_ClientId",
                table: "EFZombieClientStat",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStat_MatchId",
                table: "EFZombieClientStat",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatRecord_ClientId",
                table: "EFZombieClientStatRecord",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatRecord_RoundId",
                table: "EFZombieClientStatRecord",
                column: "RoundId");

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
                name: "IX_EFZombieMatch_MapId",
                table: "EFZombieMatch",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatch_ServerId",
                table: "EFZombieMatch",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFZombieAggregateClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieClientStatRecord");

            migrationBuilder.DropTable(
                name: "EFZombieEvents");

            migrationBuilder.DropTable(
                name: "EFZombieMatchClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieRoundClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieClientStat");

            migrationBuilder.DropTable(
                name: "EFZombieMatch");

            migrationBuilder.DropColumn(
                name: "PerformanceBucket",
                table: "EFServers");

            migrationBuilder.DropColumn(
                name: "PerformanceBucket",
                table: "EFClientRankingHistory");
        }
    }
}
