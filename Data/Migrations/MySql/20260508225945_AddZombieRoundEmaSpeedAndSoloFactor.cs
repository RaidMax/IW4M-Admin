using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
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
                type: "double",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "AverageSoloFactor",
                table: "EFZombieClientStatAggregates",
                type: "double",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.CreateTable(
                name: "EFZombieRoundDurationEmas",
                columns: table => new
                {
                    MapId = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    PlayerCount = table.Column<int>(type: "int", nullable: false),
                    EmaSeconds = table.Column<double>(type: "double", nullable: false),
                    SampleCount = table.Column<long>(type: "bigint", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");
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
