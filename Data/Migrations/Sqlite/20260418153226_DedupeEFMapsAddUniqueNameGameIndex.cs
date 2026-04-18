using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
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
                UPDATE ""EFServerSnapshot""
                SET ""MapId"" = (
                    SELECT MIN(canonical.""MapId"")
                    FROM ""EFMaps"" canonical
                    INNER JOIN ""EFMaps"" current ON canonical.""Name"" = current.""Name""
                                                 AND canonical.""Game"" = current.""Game""
                    WHERE current.""MapId"" = ""EFServerSnapshot"".""MapId""
                )
                WHERE ""MapId"" IS NOT NULL;");

            // Re-point EFZombieMatches.MapId onto the canonical MapId.
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
