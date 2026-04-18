using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class DedupeEFMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Merge pre-existing duplicate EFMaps rows that share (Name, Game). These were
            // produced by the pre-fix race in ServerDataCollector.GetOrCreateMap — two
            // concurrent server scans both inserting the same (Name, Game) row.
            // Re-point FK references onto the canonical (lowest) MapId per group, then
            // delete the orphans. No schema change; data cleanup only.

            migrationBuilder.Sql(@"
                UPDATE `EFServerSnapshot` s
                INNER JOIN `EFMaps` m ON s.`MapId` = m.`MapId`
                INNER JOIN (
                    SELECT `Name`, `Game`, MIN(`MapId`) AS canonical_id
                    FROM `EFMaps`
                    GROUP BY `Name`, `Game`
                ) c ON m.`Name` = c.`Name` AND m.`Game` = c.`Game`
                SET s.`MapId` = c.canonical_id
                WHERE m.`MapId` <> c.canonical_id;");

            migrationBuilder.Sql(@"
                UPDATE `EFZombieMatches` z
                INNER JOIN `EFMaps` m ON z.`MapId` = m.`MapId`
                INNER JOIN (
                    SELECT `Name`, `Game`, MIN(`MapId`) AS canonical_id
                    FROM `EFMaps`
                    GROUP BY `Name`, `Game`
                ) c ON m.`Name` = c.`Name` AND m.`Game` = c.`Game`
                SET z.`MapId` = c.canonical_id
                WHERE m.`MapId` <> c.canonical_id;");

            // MySQL forbids self-referencing DELETE with an unbuffered subquery — wrap it.
            migrationBuilder.Sql(@"
                DELETE FROM `EFMaps`
                WHERE `MapId` NOT IN (
                    SELECT `MapId` FROM (
                        SELECT MIN(`MapId`) AS `MapId`
                        FROM `EFMaps`
                        GROUP BY `Name`, `Game`
                    ) keepers
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dedupe is not reversible — the orphan rows are gone.
        }
    }
}
