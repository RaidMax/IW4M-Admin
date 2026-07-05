using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class RenamePerformanceBucketsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFClientHitStatistics_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientHitStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_EFClientRankingHistory_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientRankingHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_EFServers_PerformanceBuckets_PerformanceBucketId",
                table: "EFServers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PerformanceBuckets",
                table: "PerformanceBuckets");

            migrationBuilder.RenameTable(
                name: "PerformanceBuckets",
                newName: "EFPerformanceBuckets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EFPerformanceBuckets",
                table: "EFPerformanceBuckets",
                column: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientHitStatistics_EFPerformanceBuckets_PerformanceBucket~",
                table: "EFClientHitStatistics",
                column: "PerformanceBucketId",
                principalTable: "EFPerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientRankingHistory_EFPerformanceBuckets_PerformanceBucke~",
                table: "EFClientRankingHistory",
                column: "PerformanceBucketId",
                principalTable: "EFPerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFServers_EFPerformanceBuckets_PerformanceBucketId",
                table: "EFServers",
                column: "PerformanceBucketId",
                principalTable: "EFPerformanceBuckets",
                principalColumn: "PerformanceBucketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFClientHitStatistics_EFPerformanceBuckets_PerformanceBucket~",
                table: "EFClientHitStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_EFClientRankingHistory_EFPerformanceBuckets_PerformanceBucke~",
                table: "EFClientRankingHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_EFServers_EFPerformanceBuckets_PerformanceBucketId",
                table: "EFServers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EFPerformanceBuckets",
                table: "EFPerformanceBuckets");

            migrationBuilder.RenameTable(
                name: "EFPerformanceBuckets",
                newName: "PerformanceBuckets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PerformanceBuckets",
                table: "PerformanceBuckets",
                column: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientHitStatistics_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientHitStatistics",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFClientRankingHistory_PerformanceBuckets_PerformanceBucketId",
                table: "EFClientRankingHistory",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_EFServers_PerformanceBuckets_PerformanceBucketId",
                table: "EFServers",
                column: "PerformanceBucketId",
                principalTable: "PerformanceBuckets",
                principalColumn: "PerformanceBucketId");
        }
    }
}
