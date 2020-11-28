using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class AddRatingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
