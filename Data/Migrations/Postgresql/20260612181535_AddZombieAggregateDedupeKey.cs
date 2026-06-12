using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddZombieAggregateDedupeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DedupeClientId",
                table: "EFZombieClientStatAggregates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DedupeServerId",
                table: "EFZombieClientStatAggregates",
                type: "bigint",
                nullable: false,
                defaultValue: -1L);

            // Backfill the dedupe key from the TPT base table (ClientId lives there).
            migrationBuilder.Sql("""
                UPDATE "EFZombieClientStatAggregates" a
                SET "DedupeClientId" = b."ClientId",
                    "DedupeServerId" = COALESCE(a."ServerId", -1)
                FROM "EFZombieClientStats" b
                WHERE b."ZombieClientStatId" = a."ZombieClientStatId";
                """);

            // Remove duplicate aggregate rows (twin-race artifacts) before the unique
            // index lands — keep the lowest id per key, the row UpsertAggregate's
            // dedup-collapse also treats as canonical. Deleting the BASE row cascades
            // to this child table. Values on the survivor may be stale; a zmrebuild
            // recompute reconciles them.
            migrationBuilder.Sql("""
                DELETE FROM "EFZombieClientStats"
                WHERE "ZombieClientStatId" IN (
                    SELECT a."ZombieClientStatId"
                    FROM "EFZombieClientStatAggregates" a
                    WHERE a."ZombieClientStatId" NOT IN (
                        SELECT MIN(a2."ZombieClientStatId")
                        FROM "EFZombieClientStatAggregates" a2
                        GROUP BY a2."DedupeClientId", a2."DedupeServerId"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieClientStatAggregates_DedupeKey",
                table: "EFZombieClientStatAggregates",
                columns: new[] { "DedupeClientId", "DedupeServerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieClientStatAggregates_DedupeKey",
                table: "EFZombieClientStatAggregates");

            migrationBuilder.DropColumn(
                name: "DedupeClientId",
                table: "EFZombieClientStatAggregates");

            migrationBuilder.DropColumn(
                name: "DedupeServerId",
                table: "EFZombieClientStatAggregates");
        }
    }
}
