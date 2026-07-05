using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddZombieStitchingAndAssistedRoundsAndSqliteDateTimeOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieMatches_ServerId",
                table: "EFZombieMatches");

            migrationBuilder.AlterColumn<long>(
                name: "StartTime",
                table: "EFZombieRoundClientStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "EndTime",
                table: "EFZombieRoundClientStats",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "MatchStartDate",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "MatchEndDate",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "GameMatchId",
                table: "EFZombieMatches",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssistedRounds",
                table: "EFZombieMatchClientStats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoloFromRound",
                table: "EFZombieMatchClientStats",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFZombieEvents",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFZombieEvents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFZombieClientStats",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFZombieClientStats",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFZombieClientStatRecords",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFZombieClientStatRecords",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFClientStatTagValues",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFClientStatTagValues",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedDateTime",
                table: "EFClientStatTags",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CreatedDateTime",
                table: "EFClientStatTags",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_ServerId_GameMatchId_MatchEndDate",
                table: "EFZombieMatches",
                columns: new[] { "ServerId", "GameMatchId", "MatchEndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFZombieMatches_ServerId_GameMatchId_MatchEndDate",
                table: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "GameMatchId",
                table: "EFZombieMatches");

            migrationBuilder.DropColumn(
                name: "AssistedRounds",
                table: "EFZombieMatchClientStats");

            migrationBuilder.DropColumn(
                name: "SoloFromRound",
                table: "EFZombieMatchClientStats");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartTime",
                table: "EFZombieRoundClientStats",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndTime",
                table: "EFZombieRoundClientStats",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFZombieMatches",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "MatchStartDate",
                table: "EFZombieMatches",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "MatchEndDate",
                table: "EFZombieMatches",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFZombieMatches",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFZombieEvents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFZombieEvents",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFZombieClientStats",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFZombieClientStats",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFZombieClientStatRecords",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFZombieClientStatRecords",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFClientStatTagValues",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFClientStatTagValues",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedDateTime",
                table: "EFClientStatTags",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedDateTime",
                table: "EFClientStatTags",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_EFZombieMatches_ServerId",
                table: "EFZombieMatches",
                column: "ServerId");
        }
    }
}
