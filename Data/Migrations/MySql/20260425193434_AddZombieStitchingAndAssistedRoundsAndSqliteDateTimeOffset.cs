using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddZombieStitchingAndAssistedRoundsAndSqliteDateTimeOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieMatches_ServerId",
                table: "EFZombieMatches");

            migrationBuilder.AddColumn<string>(
                name: "GameMatchId",
                table: "EFZombieMatches",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AssistedRounds",
                table: "EFZombieMatchClientStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoloFromRound",
                table: "EFZombieMatchClientStats",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_ServerId_GameMatchId_MatchEndDate",
                table: "EFZombieMatches",
                columns: new[] { "ServerId", "GameMatchId", "MatchEndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieMatches_ServerId_GameMatchId_MatchEndDate",
                table: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "GameMatchId",
                table: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "AssistedRounds",
                table: "EFZombieMatchClientStats");

            migrationBuilder.DropColumn(
                name: "SoloFromRound",
                table: "EFZombieMatchClientStats");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_ServerId",
                table: "EFZombieMatches",
                column: "ServerId");
        }
    }
}
