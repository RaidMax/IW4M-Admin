using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
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
                    AnnouncementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "varchar(4096)", maxLength: 4096, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsGlobalNotice = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedByClientId = table.Column<int>(type: "int", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
