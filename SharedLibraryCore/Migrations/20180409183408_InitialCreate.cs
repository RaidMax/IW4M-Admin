using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFAliasLinks",
                columns: table => new
                {
                    AliasLinkId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFAliasLinks", x => x.AliasLinkId);
                });

            migrationBuilder.CreateTable(
                name: "EFServers",
                columns: table => new
                {
                    ServerId = table.Column<int>(nullable: false),
                    Active = table.Column<bool>(nullable: false),
                    Port = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFServers", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "Vector3",
                columns: table => new
                {
                    Vector3Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    X = table.Column<float>(nullable: false),
                    Y = table.Column<float>(nullable: false),
                    Z = table.Column<float>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vector3", x => x.Vector3Id);
                });

            migrationBuilder.CreateTable(
                name: "EFAlias",
                columns: table => new
                {
                    AliasId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: false),
                    IPAddress = table.Column<int>(nullable: false),
                    LinkId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFAlias", x => x.AliasId);
                    table.ForeignKey(
                        name: "FK_EFAlias_EFAliasLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "EFAliasLinks",
                        principalColumn: "AliasLinkId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EFServerStatistics",
                columns: table => new
                {
                    StatisticId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    ServerId = table.Column<int>(nullable: false),
                    TotalKills = table.Column<long>(nullable: false),
                    TotalPlayTime = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFServerStatistics", x => x.StatisticId);
                    table.ForeignKey(
                        name: "FK_EFServerStatistics_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFClients",
                columns: table => new
                {
                    ClientId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    AliasLinkId = table.Column<int>(nullable: false),
                    Connections = table.Column<int>(nullable: false),
                    CurrentAliasId = table.Column<int>(nullable: false),
                    FirstConnection = table.Column<DateTime>(nullable: false),
                    LastConnection = table.Column<DateTime>(nullable: false),
                    Level = table.Column<int>(nullable: false),
                    Masked = table.Column<bool>(nullable: false),
                    NetworkId = table.Column<long>(nullable: false),
                    Password = table.Column<string>(nullable: true),
                    PasswordSalt = table.Column<string>(nullable: true),
                    TotalConnectionTime = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClients", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_EFClients_EFAliasLinks_AliasLinkId",
                        column: x => x.AliasLinkId,
                        principalTable: "EFAliasLinks",
                        principalColumn: "AliasLinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClients_EFAlias_CurrentAliasId",
                        column: x => x.CurrentAliasId,
                        principalTable: "EFAlias",
                        principalColumn: "AliasId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFClientKills",
                columns: table => new
                {
                    KillId = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    AttackerId = table.Column<int>(nullable: false),
                    Damage = table.Column<int>(nullable: false),
                    DeathOriginVector3Id = table.Column<int>(nullable: true),
                    DeathType = table.Column<int>(nullable: false),
                    HitLoc = table.Column<int>(nullable: false),
                    KillOriginVector3Id = table.Column<int>(nullable: true),
                    Map = table.Column<int>(nullable: false),
                    ServerId = table.Column<int>(nullable: false),
                    VictimId = table.Column<int>(nullable: false),
                    ViewAnglesVector3Id = table.Column<int>(nullable: true),
                    Weapon = table.Column<int>(nullable: false),
                    When = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientKills", x => x.KillId);
                    table.ForeignKey(
                        name: "FK_EFClientKills_EFClients_AttackerId",
                        column: x => x.AttackerId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKills_Vector3_DeathOriginVector3Id",
                        column: x => x.DeathOriginVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFClientKills_Vector3_KillOriginVector3Id",
                        column: x => x.KillOriginVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFClientKills_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKills_EFClients_VictimId",
                        column: x => x.VictimId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKills_Vector3_ViewAnglesVector3Id",
                        column: x => x.ViewAnglesVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EFClientMessages",
                columns: table => new
                {
                    MessageId = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    ClientId = table.Column<int>(nullable: false),
                    Message = table.Column<string>(nullable: true),
                    ServerId = table.Column<int>(nullable: false),
                    TimeSent = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_EFClientMessages_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientMessages_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFClientStatistics",
                columns: table => new
                {
                    ClientId = table.Column<int>(nullable: false),
                    ServerId = table.Column<int>(nullable: false),
                    Active = table.Column<bool>(nullable: false),
                    Deaths = table.Column<int>(nullable: false),
                    Kills = table.Column<int>(nullable: false),
                    SPM = table.Column<double>(nullable: false),
                    Skill = table.Column<double>(nullable: false),
                    TimePlayed = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientStatistics", x => new { x.ClientId, x.ServerId });
                    table.ForeignKey(
                        name: "FK_EFClientStatistics_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientStatistics_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFPenalties",
                columns: table => new
                {
                    PenaltyId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    Expires = table.Column<DateTime>(nullable: false),
                    LinkId = table.Column<int>(nullable: false),
                    OffenderId = table.Column<int>(nullable: false),
                    Offense = table.Column<string>(nullable: false),
                    PunisherId = table.Column<int>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    When = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFPenalties", x => x.PenaltyId);
                    table.ForeignKey(
                        name: "FK_EFPenalties_EFAliasLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "EFAliasLinks",
                        principalColumn: "AliasLinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFPenalties_EFClients_OffenderId",
                        column: x => x.OffenderId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFPenalties_EFClients_PunisherId",
                        column: x => x.PunisherId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EFHitLocationCounts",
                columns: table => new
                {
                    HitLocationCountId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    EFClientStatistics_ClientId = table.Column<int>(nullable: false),
                    HitCount = table.Column<int>(nullable: false),
                    HitOffsetAverage = table.Column<float>(nullable: false),
                    Location = table.Column<int>(nullable: false),
                    EFClientStatistics_ServerId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFHitLocationCounts", x => x.HitLocationCountId);
                    table.ForeignKey(
                        name: "FK_EFHitLocationCounts_EFClients_EFClientStatistics_ClientId",
                        column: x => x.EFClientStatistics_ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFHitLocationCounts_EFServers_EFClientStatistics_ServerId",
                        column: x => x.EFClientStatistics_ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatistics_ClientId_EFClientStatistics_ServerId",
                        columns: x => new { x.EFClientStatistics_ClientId, x.EFClientStatistics_ServerId },
                        principalTable: "EFClientStatistics",
                        principalColumns: new[] { "ClientId", "ServerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_LinkId",
                table: "EFAlias",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_AttackerId",
                table: "EFClientKills",
                column: "AttackerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_DeathOriginVector3Id",
                table: "EFClientKills",
                column: "DeathOriginVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_KillOriginVector3Id",
                table: "EFClientKills",
                column: "KillOriginVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_ServerId",
                table: "EFClientKills",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_VictimId",
                table: "EFClientKills",
                column: "VictimId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKills_ViewAnglesVector3Id",
                table: "EFClientKills",
                column: "ViewAnglesVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientMessages_ClientId",
                table: "EFClientMessages",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientMessages_ServerId",
                table: "EFClientMessages",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClients_AliasLinkId",
                table: "EFClients",
                column: "AliasLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClients_CurrentAliasId",
                table: "EFClients",
                column: "CurrentAliasId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClients_NetworkId",
                table: "EFClients",
                column: "NetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatistics_ServerId",
                table: "EFClientStatistics",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFHitLocationCounts_EFClientStatistics_ServerId",
                table: "EFHitLocationCounts",
                column: "EFClientStatistics_ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFHitLocationCounts_EFClientStatistics_ClientId_EFClientStatistics_ServerId",
                table: "EFHitLocationCounts",
                columns: new[] { "EFClientStatistics_ClientId", "EFClientStatistics_ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_EFPenalties_LinkId",
                table: "EFPenalties",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_EFPenalties_OffenderId",
                table: "EFPenalties",
                column: "OffenderId");

            migrationBuilder.CreateIndex(
                name: "IX_EFPenalties_PunisherId",
                table: "EFPenalties",
                column: "PunisherId");

            migrationBuilder.CreateIndex(
                name: "IX_EFServerStatistics_ServerId",
                table: "EFServerStatistics",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFClientKills");

            migrationBuilder.DropTable(
                name: "EFClientMessages");

            migrationBuilder.DropTable(
                name: "EFHitLocationCounts");

            migrationBuilder.DropTable(
                name: "EFPenalties");

            migrationBuilder.DropTable(
                name: "EFServerStatistics");

            migrationBuilder.DropTable(
                name: "Vector3");

            migrationBuilder.DropTable(
                name: "EFClientStatistics");

            migrationBuilder.DropTable(
                name: "EFClients");

            migrationBuilder.DropTable(
                name: "EFServers");

            migrationBuilder.DropTable(
                name: "EFAlias");

            migrationBuilder.DropTable(
                name: "EFAliasLinks");
        }
    }
}
