using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddEFClientConnectionHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFClientConnectionHistory",
                columns: table => new
                {
                    ClientConnectionId = table.Column<long>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatedDateTime = table.Column<DateTime>(nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(nullable: true),
                    ClientId = table.Column<int>(nullable: false),
                    ServerId = table.Column<long>(nullable: false),
                    ConnectionType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientConnectionHistory", x => x.ClientConnectionId);
                    table.ForeignKey(
                        name: "FK_EFClientConnectionHistory_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientConnectionHistory_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFClientConnectionHistory_ClientId",
                table: "EFClientConnectionHistory",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientConnectionHistory_CreatedDateTime",
                table: "EFClientConnectionHistory",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientConnectionHistory_ServerId",
                table: "EFClientConnectionHistory",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFClientConnectionHistory");
        }
    }
}
