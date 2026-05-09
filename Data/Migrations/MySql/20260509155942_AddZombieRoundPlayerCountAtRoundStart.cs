using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddZombieRoundPlayerCountAtRoundStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayerCountAtRoundStart",
                table: "EFZombieRoundClientStats",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerCountAtRoundStart",
                table: "EFZombieRoundClientStats");
        }
    }
}
