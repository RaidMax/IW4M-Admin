using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddZombieMatchEasterEgg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EasterEggOccurredAt",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EasterEggRound",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EasterEggOccurredAt",
                table: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "EasterEggRound",
                table: "EFZombieMatches");
        }
    }
}
