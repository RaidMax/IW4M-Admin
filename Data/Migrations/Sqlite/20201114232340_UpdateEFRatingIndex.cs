using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class UpdateEFRatingIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFRating_When_ServerId_Performance_ActivityAmount",
                table: "EFRating",
                columns: new[] { "When", "ServerId", "Performance", "ActivityAmount" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFRating_When_ServerId_Performance_ActivityAmount",
                table: "EFRating");
        }
    }
}
