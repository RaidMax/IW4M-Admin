using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations
{
    public partial class AddEFACSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EFACSnapshotSnapshotId",
                table: "Vector3",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFACSnapshot",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    ClientId = table.Column<int>(nullable: false),
                    CurrentSessionLength = table.Column<int>(nullable: false),
                    CurrentStrain = table.Column<double>(nullable: false),
                    CurrentViewAngleVector3Id = table.Column<int>(nullable: true),
                    Deaths = table.Column<int>(nullable: false),
                    Distance = table.Column<double>(nullable: false),
                    EloRating = table.Column<double>(nullable: false),
                    HitDestinationVector3Id = table.Column<int>(nullable: true),
                    HitLocation = table.Column<int>(nullable: false),
                    HitOriginVector3Id = table.Column<int>(nullable: true),
                    HitType = table.Column<int>(nullable: false),
                    Hits = table.Column<int>(nullable: false),
                    Kills = table.Column<int>(nullable: false),
                    LastStrainAngleVector3Id = table.Column<int>(nullable: true),
                    SessionAngleOffset = table.Column<double>(nullable: false),
                    SessionSPM = table.Column<double>(nullable: false),
                    SessionScore = table.Column<int>(nullable: false),
                    StrainAngleBetween = table.Column<double>(nullable: false),
                    TimeSinceLastEvent = table.Column<int>(nullable: false),
                    WeaponId = table.Column<int>(nullable: false),
                    When = table.Column<DateTime>(nullable: false)
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
                        name: "FK_EFACSnapshot_Vector3_CurrentViewAngleVector3Id",
                        column: x => x.CurrentViewAngleVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_HitDestinationVector3Id",
                        column: x => x.HitDestinationVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_HitOriginVector3Id",
                        column: x => x.HitOriginVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFACSnapshot_Vector3_LastStrainAngleVector3Id",
                        column: x => x.LastStrainAngleVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vector3_EFACSnapshotSnapshotId",
                table: "Vector3",
                column: "EFACSnapshotSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_ClientId",
                table: "EFACSnapshot",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_CurrentViewAngleVector3Id",
                table: "EFACSnapshot",
                column: "CurrentViewAngleVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_HitDestinationVector3Id",
                table: "EFACSnapshot",
                column: "HitDestinationVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_HitOriginVector3Id",
                table: "EFACSnapshot",
                column: "HitOriginVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshot_LastStrainAngleVector3Id",
                table: "EFACSnapshot",
                column: "LastStrainAngleVector3Id");

           /* migrationBuilder.AddForeignKey(
                name: "FK_Vector3_EFACSnapshot_EFACSnapshotSnapshotId",
                table: "Vector3",
                column: "EFACSnapshotSnapshotId",
                principalTable: "EFACSnapshot",
                principalColumn: "SnapshotId",
                onDelete: ReferentialAction.Restrict);*/
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
