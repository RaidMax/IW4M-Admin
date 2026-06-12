using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
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
                type: "int",
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
                UPDATE EFZombieClientStatAggregates a
                JOIN EFZombieClientStats b ON b.ZombieClientStatId = a.ZombieClientStatId
                SET a.DedupeClientId = b.ClientId,
                    a.DedupeServerId = COALESCE(a.ServerId, -1);
                """);

            // Remove duplicate aggregate rows (twin-race artifacts) before the unique
            // index lands — keep the lowest id per key. Deleting the BASE row cascades
            // to this child table. Survivor values may be stale; zmrebuild reconciles.
            // (Subqueries target the aggregates table, not the table being deleted
            // from, so MySQL's same-table DELETE restriction doesn't apply.)
            migrationBuilder.Sql("""
                DELETE FROM EFZombieClientStats
                WHERE ZombieClientStatId IN (
                    SELECT ZombieClientStatId FROM (
                        SELECT a.ZombieClientStatId
                        FROM EFZombieClientStatAggregates a
                        JOIN (
                            SELECT DedupeClientId, DedupeServerId, MIN(ZombieClientStatId) AS KeepId
                            FROM EFZombieClientStatAggregates
                            GROUP BY DedupeClientId, DedupeServerId
                        ) k ON k.DedupeClientId = a.DedupeClientId
                           AND k.DedupeServerId = a.DedupeServerId
                           AND a.ZombieClientStatId <> k.KeepId
                    ) doomed);
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
