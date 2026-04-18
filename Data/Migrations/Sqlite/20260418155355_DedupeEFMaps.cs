using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
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
                UPDATE ""EFServerSnapshot""
                SET ""MapId"" = (
                    SELECT MIN(canonical.""MapId"")
                    FROM ""EFMaps"" canonical
                    INNER JOIN ""EFMaps"" current ON canonical.""Name"" = current.""Name""
                                                 AND canonical.""Game"" = current.""Game""
                    WHERE current.""MapId"" = ""EFServerSnapshot"".""MapId""
                )
                WHERE ""MapId"" IS NOT NULL;");

            migrationBuilder.Sql(@"
                UPDATE ""EFZombieMatches""
                SET ""MapId"" = (
                    SELECT MIN(canonical.""MapId"")
                    FROM ""EFMaps"" canonical
                    INNER JOIN ""EFMaps"" current ON canonical.""Name"" = current.""Name""
                                                 AND canonical.""Game"" = current.""Game""
                    WHERE current.""MapId"" = ""EFZombieMatches"".""MapId""
                )
                WHERE ""MapId"" IS NOT NULL;");

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
