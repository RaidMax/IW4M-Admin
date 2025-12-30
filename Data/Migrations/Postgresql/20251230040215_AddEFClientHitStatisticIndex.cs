using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddEFClientHitStatisticIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*migrationBuilder.DropIndex(
                name: "IX_EFClientHitStatistics_ClientId",
                table: "EFClientHitStatistics");*/

            migrationBuilder.CreateIndex(
                name: "IX_EFClientHitStatistics_ClientId_ServerId",
                table: "EFClientHitStatistics",
                columns: new[] { "ClientId", "ServerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFClientHitStatistics_ClientId_ServerId",
                table: "EFClientHitStatistics");

            migrationBuilder.CreateIndex(
                name: "IX_EFClientHitStatistics_ClientId",
                table: "EFClientHitStatistics",
                column: "ClientId");
        }
    }
}
