using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddEFPenaltyIdentifier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFPenaltyIdentifiers",
                columns: table => new
                {
                    PenaltyIdentifierId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IPv4Address = table.Column<int>(type: "INTEGER", nullable: true),
                    NetworkId = table.Column<long>(type: "INTEGER", nullable: false),
                    PenaltyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFPenaltyIdentifiers", x => x.PenaltyIdentifierId);
                    table.ForeignKey(
                        name: "FK_EFPenaltyIdentifiers_EFPenalties_PenaltyId",
                        column: x => x.PenaltyId,
                        principalTable: "EFPenalties",
                        principalColumn: "PenaltyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFPenaltyIdentifiers_IPv4Address",
                table: "EFPenaltyIdentifiers",
                column: "IPv4Address");

            migrationBuilder.CreateIndex(
                name: "IX_EFPenaltyIdentifiers_NetworkId",
                table: "EFPenaltyIdentifiers",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_EFPenaltyIdentifiers_PenaltyId",
                table: "EFPenaltyIdentifiers",
                column: "PenaltyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFPenaltyIdentifiers");
        }
    }
}
