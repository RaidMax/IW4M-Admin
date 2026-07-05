using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddZombieMatchCompletedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "EFZombieMatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completed",
                table: "EFZombieMatches");
        }
    }
}
