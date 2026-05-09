using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddZombieRoundEmaSpeedAndSoloFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRelativeSpeed",
                table: "EFZombieClientStatAggregates",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "AverageSoloFactor",
                table: "EFZombieClientStatAggregates",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.CreateTable(
                name: "EFZombieRoundDurationEmas",
                columns: table => new
                {
                    MapId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EmaSeconds = table.Column<double>(type: "REAL", nullable: false),
                    SampleCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFZombieRoundDurationEmas", x => new { x.MapId, x.RoundNumber, x.PlayerCount });
                    table.ForeignKey(
                        name: "FK_EFZombieRoundDurationEmas_EFMaps_MapId",
                        column: x => x.MapId,
                        principalTable: "EFMaps",
                        principalColumn: "MapId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFZombieRoundDurationEmas");

            migrationBuilder.DropColumn(
                name: "AverageRelativeSpeed",
                table: "EFZombieClientStatAggregates");

            migrationBuilder.DropColumn(
                name: "AverageSoloFactor",
                table: "EFZombieClientStatAggregates");
        }
    }
}
