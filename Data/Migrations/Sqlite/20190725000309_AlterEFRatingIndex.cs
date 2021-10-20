using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class AlterEFRatingIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFRating_Performance",
                table: "EFRating");

            migrationBuilder.DropIndex(
                name: "IX_EFRating_Ranking",
                table: "EFRating");

            migrationBuilder.DropIndex(
                name: "IX_EFRating_When",
                table: "EFRating");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_Performance_Ranking_When",
                table: "EFRating",
                columns: new[] { "Performance", "Ranking", "When" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFRating_Performance_Ranking_When",
                table: "EFRating");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_Performance",
                table: "EFRating",
                column: "Performance");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_Ranking",
                table: "EFRating",
                column: "Ranking");

            migrationBuilder.CreateIndex(
                name: "IX_EFRating_When",
                table: "EFRating",
                column: "When");
        }
    }
}
