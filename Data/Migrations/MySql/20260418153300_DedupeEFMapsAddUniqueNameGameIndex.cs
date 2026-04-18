using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
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
                UPDATE `EFServerSnapshot` s
                INNER JOIN `EFMaps` m ON s.`MapId` = m.`MapId`
                INNER JOIN (
                    SELECT `Name`, `Game`, MIN(`MapId`) AS canonical_id
                    FROM `EFMaps`
                    GROUP BY `Name`, `Game`
                ) c ON m.`Name` = c.`Name` AND m.`Game` = c.`Game`
                SET s.`MapId` = c.canonical_id
                WHERE m.`MapId` <> c.canonical_id;");

            // Re-point EFZombieMatches.MapId onto the canonical MapId.
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

            // Delete the now-orphan EFMaps rows. Wrap the subquery so MySQL does not
            // refuse the self-referencing DELETE.
            migrationBuilder.Sql(@"
                DELETE FROM `EFMaps`
                WHERE `MapId` NOT IN (
                    SELECT `MapId` FROM (
                        SELECT MIN(`MapId`) AS `MapId`
                        FROM `EFMaps`
                        GROUP BY `Name`, `Game`
                    ) keepers
                );");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EFMaps",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EFMaps",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
