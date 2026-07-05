using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
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
                WITH canonical AS (
                    SELECT ""Name"", ""Game"", MIN(""MapId"") AS canonical_id
                    FROM ""EFMaps""
                    GROUP BY ""Name"", ""Game""
                )
                UPDATE ""EFServerSnapshot"" s
                SET ""MapId"" = c.canonical_id
                FROM ""EFMaps"" m
                INNER JOIN canonical c ON m.""Name"" = c.""Name"" AND m.""Game"" = c.""Game""
                WHERE s.""MapId"" = m.""MapId"" AND m.""MapId"" <> c.canonical_id;");

            migrationBuilder.Sql(@"
                WITH canonical AS (
                    SELECT ""Name"", ""Game"", MIN(""MapId"") AS canonical_id
                    FROM ""EFMaps""
                    GROUP BY ""Name"", ""Game""
                )
                UPDATE ""EFZombieMatches"" z
                SET ""MapId"" = c.canonical_id
                FROM ""EFMaps"" m
                INNER JOIN canonical c ON m.""Name"" = c.""Name"" AND m.""Game"" = c.""Game""
                WHERE z.""MapId"" = m.""MapId"" AND m.""MapId"" <> c.canonical_id;");

            migrationBuilder.Sql(@"
                DELETE FROM ""EFMaps""
                WHERE ""MapId"" NOT IN (
                    SELECT MIN(""MapId"") FROM ""EFMaps"" GROUP BY ""Name"", ""Game""
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dedupe is not reversible — the orphan rows are gone.
        }
    }
}
