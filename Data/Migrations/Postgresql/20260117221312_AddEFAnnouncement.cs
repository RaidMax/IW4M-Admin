using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddEFAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EFAnnouncement",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsGlobalNotice = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByClientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFAnnouncement", x => x.AnnouncementId);
                    table.ForeignKey(
                        name: "FK_EFAnnouncement_EFClients_CreatedByClientId",
                        column: x => x.CreatedByClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFAnnouncement_CreatedByClientId",
                table: "EFAnnouncement",
                column: "CreatedByClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFAnnouncement_IsActive",
                table: "EFAnnouncement",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EFAnnouncement_IsGlobalNotice",
                table: "EFAnnouncement",
                column: "IsGlobalNotice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFAnnouncement");
        }
    }
}
