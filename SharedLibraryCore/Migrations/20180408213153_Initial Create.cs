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
                name: "AliasLinks",
                columns: table => new
                {
                    AliasLinkId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AliasLinks", x => x.AliasLinkId);
                });

            migrationBuilder.CreateTable(
                name: "EFServer",
                columns: table => new
                {
                    ServerId = table.Column<int>(nullable: false),
                    Active = table.Column<bool>(nullable: false),
                    Port = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFServer", x => x.ServerId);
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
                name: "Aliases",
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
                    table.PrimaryKey("PK_Aliases", x => x.AliasId);
                    table.ForeignKey(
                        name: "FK_Aliases_AliasLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "AliasLinks",
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
                        name: "FK_EFServerStatistics_EFServer_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServer",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
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
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_Clients_AliasLinks_AliasLinkId",
                        column: x => x.AliasLinkId,
                        principalTable: "AliasLinks",
                        principalColumn: "AliasLinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Clients_Aliases_CurrentAliasId",
                        column: x => x.CurrentAliasId,
                        principalTable: "Aliases",
                        principalColumn: "AliasId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFClientKill",
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
                    table.PrimaryKey("PK_EFClientKill", x => x.KillId);
                    table.ForeignKey(
                        name: "FK_EFClientKill_Clients_AttackerId",
                        column: x => x.AttackerId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKill_Vector3_DeathOriginVector3Id",
                        column: x => x.DeathOriginVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFClientKill_Vector3_KillOriginVector3Id",
                        column: x => x.KillOriginVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EFClientKill_EFServer_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServer",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKill_Clients_VictimId",
                        column: x => x.VictimId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientKill_Vector3_ViewAnglesVector3Id",
                        column: x => x.ViewAnglesVector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EFClientMessage",
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
                    table.PrimaryKey("PK_EFClientMessage", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_EFClientMessage_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientMessage_EFServer_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServer",
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
                        name: "FK_EFClientStatistics_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFClientStatistics_EFServer_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServer",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Penalties",
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
                    table.PrimaryKey("PK_Penalties", x => x.PenaltyId);
                    table.ForeignKey(
                        name: "FK_Penalties_AliasLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "AliasLinks",
                        principalColumn: "AliasLinkId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Penalties_Clients_OffenderId",
                        column: x => x.OffenderId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Penalties_Clients_PunisherId",
                        column: x => x.PunisherId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EFHitLocationCount",
                columns: table => new
                {
                    HitLocationCountId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Active = table.Column<bool>(nullable: false),
                    EFClientStatisticsClientId = table.Column<int>(nullable: true),
                    EFClientStatisticsServerId = table.Column<int>(nullable: true),
                    HitCount = table.Column<int>(nullable: false),
                    HitOffsetAverage = table.Column<float>(nullable: false),
                    Location = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFHitLocationCount", x => x.HitLocationCountId);
                    table.ForeignKey(
                        name: "FK_EFHitLocationCount_EFClientStatistics_EFClientStatisticsClientId_EFClientStatisticsServerId",
                        columns: x => new { x.EFClientStatisticsClientId, x.EFClientStatisticsServerId },
                        principalTable: "EFClientStatistics",
                        principalColumns: new[] { "ClientId", "ServerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_LinkId",
                table: "Aliases",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AliasLinkId",
                table: "Clients",
                column: "AliasLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CurrentAliasId",
                table: "Clients",
                column: "CurrentAliasId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NetworkId",
                table: "Clients",
                column: "NetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_AttackerId",
                table: "EFClientKill",
                column: "AttackerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_DeathOriginVector3Id",
                table: "EFClientKill",
                column: "DeathOriginVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_KillOriginVector3Id",
                table: "EFClientKill",
                column: "KillOriginVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_ServerId",
                table: "EFClientKill",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_VictimId",
                table: "EFClientKill",
                column: "VictimId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientKill_ViewAnglesVector3Id",
                table: "EFClientKill",
                column: "ViewAnglesVector3Id");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientMessage_ClientId",
                table: "EFClientMessage",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientMessage_ServerId",
                table: "EFClientMessage",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientStatistics_ServerId",
                table: "EFClientStatistics",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EFHitLocationCount_EFClientStatisticsClientId_EFClientStatisticsServerId",
                table: "EFHitLocationCount",
                columns: new[] { "EFClientStatisticsClientId", "EFClientStatisticsServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_EFServerStatistics_ServerId",
                table: "EFServerStatistics",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_LinkId",
                table: "Penalties",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_OffenderId",
                table: "Penalties",
                column: "OffenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_PunisherId",
                table: "Penalties",
                column: "PunisherId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFClientKill");

            migrationBuilder.DropTable(
                name: "EFClientMessage");

            migrationBuilder.DropTable(
                name: "EFHitLocationCount");

            migrationBuilder.DropTable(
                name: "EFServerStatistics");

            migrationBuilder.DropTable(
                name: "Penalties");

            migrationBuilder.DropTable(
                name: "Vector3");

            migrationBuilder.DropTable(
                name: "EFClientStatistics");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "EFServer");

            migrationBuilder.DropTable(
                name: "Aliases");

            migrationBuilder.DropTable(
                name: "AliasLinks");
        }
    }
}
