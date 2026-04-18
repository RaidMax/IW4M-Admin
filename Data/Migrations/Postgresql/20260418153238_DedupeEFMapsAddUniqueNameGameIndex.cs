using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class DedupeEFMapsAddUniqueNameGameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before adding the unique index, merge any pre-existing duplicates created by
            // the pre-fix race in ServerDataCollector.GetOrCreateMap (two concurrent server
            // scans both inserting the same (Name, Game) row). Re-point FK references onto
            // the canonical (lowest) MapId per group, then delete the orphans.

            // Re-point EFServerSnapshot.MapId onto the canonical MapId.
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

            // Re-point EFZombieMatches.MapId onto the canonical MapId.
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

            // Delete the now-orphan EFMaps rows.
            migrationBuilder.Sql(@"
                DELETE FROM ""EFMaps""
                WHERE ""MapId"" NOT IN (
                    SELECT MIN(""MapId"") FROM ""EFMaps"" GROUP BY ""Name"", ""Game""
                );");

            migrationBuilder.CreateIndex(
                name: "IX_EFMaps_Name_Game",
                table: "EFMaps",
                columns: new[] { "Name", "Game" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFMaps_Name_Game",
                table: "EFMaps");
        }
    }
}
