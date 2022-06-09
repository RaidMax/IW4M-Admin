using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    public partial class AddIndexToEFRankingHistoryCreatedDatetime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFClientRankingHistory_CreatedDateTime",
                table: "EFClientRankingHistory",
                column: "CreatedDateTime");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFClientRankingHistory_CreatedDateTime",
                table: "EFClientRankingHistory");
        }
    }
}
