using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PersistZombieRoundScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AggregatesApplied",
                table: "EFZombieRoundClientStats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RelativeSpeed",
                table: "EFZombieRoundClientStats",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JoinedRound",
                table: "EFZombieMatchClientStats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastRoundReached",
                table: "EFZombieMatchClientStats",
                type: "INTEGER",
                nullable: true);
            migrationBuilder.Sql("""
UPDATE "EFZombieMatchClientStats" SET
"JoinedRound" = (SELECT MIN(r."RoundNumber") FROM "EFZombieRoundClientStats" r
JOIN "EFZombieClientStats" rb ON rb."ZombieClientStatId" = r."ZombieClientStatId"
JOIN "EFZombieClientStats" mb ON mb."ClientId" = rb."ClientId" AND mb."MatchId" = rb."MatchId"
WHERE mb."ZombieClientStatId" = "EFZombieMatchClientStats"."ZombieClientStatId"),
"LastRoundReached" = (SELECT MAX(r."RoundNumber") FROM "EFZombieRoundClientStats" r
JOIN "EFZombieClientStats" rb ON rb."ZombieClientStatId" = r."ZombieClientStatId"
JOIN "EFZombieClientStats" mb ON mb."ClientId" = rb."ClientId" AND mb."MatchId" = rb."MatchId"
WHERE mb."ZombieClientStatId" = "EFZombieMatchClientStats"."ZombieClientStatId");
""");
            SqliteDateTimeOffsetMigration.EncodeText(migrationBuilder);
            // The 2021 SQL migration indexed the quoted string "_ServerId"
            // rather than ServerId. Repair existing databases as well as the
            // corrected historical migration used by fresh installations.
            migrationBuilder.Sql("""
DROP INDEX IF EXISTS "IX_EFACSnapshot_ServerId";
CREATE INDEX "IX_EFACSnapshot_ServerId" ON "EFACSnapshot" ("ServerId");
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AggregatesApplied",
                table: "EFZombieRoundClientStats");

            migrationBuilder.DropColumn(
                name: "RelativeSpeed",
                table: "EFZombieRoundClientStats");

            migrationBuilder.DropColumn(
                name: "JoinedRound",
                table: "EFZombieMatchClientStats");

            migrationBuilder.DropColumn(
                name: "LastRoundReached",
                table: "EFZombieMatchClientStats");
        }
    }
}
