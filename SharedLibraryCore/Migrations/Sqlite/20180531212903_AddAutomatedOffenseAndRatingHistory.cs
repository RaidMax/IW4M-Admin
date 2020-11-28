using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class AddAutomatedOffenseAndRatingHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomatedOffense",
                table: "EFPenalties",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EFClientRatingHistory",
                columns: table => new
                {
                    RatingHistoryId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Active = table.Column<bool>(nullable: false),
                    ClientId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFClientRatingHistory", x => x.RatingHistoryId);
                    table.ForeignKey(
                        name: "FK_EFClientRatingHistory_EFClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "EFClients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EFRating",
                columns: table => new
                {
                    RatingId = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Active = table.Column<bool>(nullable: false),
                    Newest = table.Column<bool>(nullable: false),
                    Performance = table.Column<double>(nullable: false),
                    Ranking = table.Column<int>(nullable: false),
                    RatingHistoryId = table.Column<int>(nullable: false),
                    ServerId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFRating", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_EFRating_EFClientRatingHistory_RatingHistoryId",
                        column: x => x.RatingHistoryId,
                        principalTable: "EFClientRatingHistory",
                        principalColumn: "RatingHistoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFRating_EFServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "EFServers",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFClientRatingHistory_ClientId",
                table: "EFClientRatingHistory",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_RatingHistoryId",
                table: "EFRating",
                column: "RatingHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_ServerId",
                table: "EFRating",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFRating");

            migrationBuilder.DropTable(
                name: "EFClientRatingHistory");

            migrationBuilder.DropColumn(
                name: "AutomatedOffense",
                table: "EFPenalties");
        }
    }
}
