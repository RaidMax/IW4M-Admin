using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddEFInboxMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    InboxMessageId = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatedDateTime = table.Column<DateTime>(nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(nullable: true),
                    SourceClientId = table.Column<int>(nullable: false),
                    DestinationClientId = table.Column<int>(nullable: false),
                    ServerId = table.Column<long>(nullable: true),
                    Message = table.Column<string>(nullable: true),
                    IsDelivered = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.InboxMessageId);
                    table.ForeignKey(
                        name: "FK_InboxMessages_EFClients_DestinationClientId",
                        column: x => x.DestinationClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InboxMessages_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InboxMessages_EFClients_SourceClientId",
                        column: x => x.SourceClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_DestinationClientId",
                table: "InboxMessages",
                column: "DestinationClientId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ServerId",
                table: "InboxMessages",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_SourceClientId",
                table: "InboxMessages",
                column: "SourceClientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");
        }
    }
}
