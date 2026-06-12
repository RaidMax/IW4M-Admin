using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DedupeServerId",
                table: "EFZombieClientStatAggregates",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1L);

            // Backfill the dedupe key from the TPT base table (ClientId lives there).
            // Correlated-subquery form: SQLite's UPDATE..FROM exists only on 3.33+,
            // and the bundled native lib version isn't guaranteed.
            migrationBuilder.Sql("""
                UPDATE EFZombieClientStatAggregates
                SET DedupeClientId = (
                        SELECT b.ClientId FROM EFZombieClientStats b
                        WHERE b.ZombieClientStatId = EFZombieClientStatAggregates.ZombieClientStatId),
                    DedupeServerId = COALESCE(ServerId, -1);
                """);

            // Remove duplicate aggregate rows (twin-race artifacts) before the unique
            // index lands — keep the lowest id per key. Deleting the BASE row cascades
            // to this child table. Survivor values may be stale; zmrebuild reconciles.
            migrationBuilder.Sql("""
                DELETE FROM EFZombieClientStats
                WHERE ZombieClientStatId IN (
                    SELECT a.ZombieClientStatId
                    FROM EFZombieClientStatAggregates a
                    WHERE a.ZombieClientStatId NOT IN (
                        SELECT MIN(a2.ZombieClientStatId)
                        FROM EFZombieClientStatAggregates a2
                        GROUP BY a2.DedupeClientId, a2.DedupeServerId));
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
