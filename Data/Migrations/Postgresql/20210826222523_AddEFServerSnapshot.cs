using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Data.Migrations.Postgresql
{
    public partial class AddEFServerSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFServerSnapshot",
                columns: table => new
                {
                    ServerSnapshotId = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    Active = table.Column<bool>(nullable: false),
                    CapturedAt = table.Column<DateTime>(nullable: false),
                    PeriodBlock = table.Column<int>(nullable: false),
                    ServerId = table.Column<long>(nullable: false),
                    MapId = table.Column<int>(nullable: false),
                    ClientCount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFServerSnapshot", x => x.ServerSnapshotId);
                    table.ForeignKey(
                        name: "FK_EFServerSnapshot_EFMaps_MapId",
                        column: x => x.MapId,
                        principalTable: "EFMaps",
                        principalColumn: "MapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFServerSnapshot_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFServerSnapshot_MapId",
                table: "EFServerSnapshot",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_EFServerSnapshot_ServerId",
                table: "EFServerSnapshot",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFServerSnapshot");
        }
    }
}
