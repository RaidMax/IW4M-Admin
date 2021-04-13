using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Data.Migrations.MySql
{
    public partial class ReaddACSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "EFACSnapshot",
                columns: table => new
                {
                    Active = table.Column<bool>(nullable: false),
                    SnapshotId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<int>(nullable: false),
                    When = table.Column<DateTime>(nullable: false),
                    CurrentSessionLength = table.Column<int>(nullable: false),
                    TimeSinceLastEvent = table.Column<int>(nullable: false),
                    EloRating = table.Column<double>(nullable: false),
                    SessionScore = table.Column<int>(nullable: false),
                    SessionSPM = table.Column<double>(nullable: false),
                    Hits = table.Column<int>(nullable: false),
                    Kills = table.Column<int>(nullable: false),
                    Deaths = table.Column<int>(nullable: false),
                    CurrentStrain = table.Column<double>(nullable: false),
                    StrainAngleBetween = table.Column<double>(nullable: false),
                    SessionAngleOffset = table.Column<double>(nullable: false),
                    LastStrainAngleId = table.Column<int>(nullable: false),
                    HitOriginId = table.Column<int>(nullable: false),
                    HitDestinationId = table.Column<int>(nullable: false),
                    Distance = table.Column<double>(nullable: false),
                    CurrentViewAngleId = table.Column<int>(nullable: true),
                    WeaponId = table.Column<int>(nullable: false),
                    HitLocation = table.Column<int>(nullable: false),
                    HitType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFACSnapshot", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_CurrentViewAngleId",
                        column: x => x.CurrentViewAngleId,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_HitDestinationId",
                        column: x => x.HitDestinationId,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_HitOriginId",
                        column: x => x.HitOriginId,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_LastStrainAngleId",
                        column: x => x.LastStrainAngleId,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Cascade);
                });

            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.AddColumn<int>(
                    name: "EFACSnapshotSnapshotId",
                    table: "Vector3",
                    nullable: true);

                migrationBuilder.AddForeignKey(
                name: "FK_Vector3_EFACSnapshot_EFACSnapshotSnapshotId",
                table: "Vector3",
                column: "EFACSnapshotSnapshotId",
                principalTable: "EFACSnapshot",
                principalColumn: "SnapshotId",
                onDelete: ReferentialAction.Restrict);

                migrationBuilder.CreateIndex(
                    name: "IX_Vector3_EFACSnapshotSnapshotId",
                    table: "Vector3",
                    column: "EFACSnapshotSnapshotId");

            }

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_ClientId",
                table: "EFACSnapshot",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_CurrentViewAngleId",
                table: "EFACSnapshot",
                column: "CurrentViewAngleId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_HitDestinationId",
                table: "EFACSnapshot",
                column: "HitDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_HitOriginId",
                table: "EFACSnapshot",
                column: "HitOriginId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_LastStrainAngleId",
                table: "EFACSnapshot",
                column: "LastStrainAngleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vector3_EFACSnapshot_EFACSnapshotSnapshotId",
                table: "Vector3");

            migrationBuilder.DropTable(
                name: "EFACSnapshot");

            migrationBuilder.DropIndex(
                name: "IX_Vector3_EFACSnapshotSnapshotId",
                table: "Vector3");

            migrationBuilder.DropColumn(
                name: "EFACSnapshotSnapshotId",
                table: "Vector3");
        }
    }
}
