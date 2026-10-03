using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite;

public partial class NormalizeZombieTimestampsToUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        SqliteDateTimeOffsetMigration.NormalizeUtc(migrationBuilder);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // UTC values are also readable by the previous binary converter. Original
        // offsets are presentation metadata and cannot be reconstructed after normalization.
    }
}
