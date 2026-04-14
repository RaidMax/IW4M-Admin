using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddZombieEconomyStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoxUses",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BuildablesCompleted",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DoorsOpened",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrapsActivated",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponsPurchased",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeaponsUpgraded",
                table: "EFZombieClientStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoxUses",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "BuildablesCompleted",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "DoorsOpened",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "TrapsActivated",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "WeaponsPurchased",
                table: "EFZombieClientStats");

            migrationBuilder.DropColumn(
                name: "WeaponsUpgraded",
                table: "EFZombieClientStats");
        }
    }
}
