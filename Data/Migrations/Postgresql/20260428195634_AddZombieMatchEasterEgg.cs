using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddZombieMatchEasterEgg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EasterEggOccurredAt",
                table: "EFZombieMatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EasterEggRound",
                table: "EFZombieMatches",
                type: "integer",
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
