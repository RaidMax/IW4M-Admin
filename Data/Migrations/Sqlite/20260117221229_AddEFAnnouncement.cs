using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
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
                    AnnouncementId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    StartAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGlobalNotice = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "TEXT", nullable: true)
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
