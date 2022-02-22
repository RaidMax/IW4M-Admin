using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations.Postgresql
{
    public partial class AddEFPenaltyIdentifier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFPenaltyIdentifiers",
                columns: table => new
                {
                    PenaltyIdentifierId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IPv4Address = table.Column<int>(type: "integer", nullable: true),
                    NetworkId = table.Column<long>(type: "bigint", nullable: false),
                    PenaltyId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
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
