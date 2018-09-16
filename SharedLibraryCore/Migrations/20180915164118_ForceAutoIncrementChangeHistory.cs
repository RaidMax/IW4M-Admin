using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class ForceAutoIncrementChangeHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // hack: we can't alter the column on SQLite, but we need max length limit for the Index in MySQL etc
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.AlterColumn<int>(
                name: "ChangeHistoryId",
                table: "EFChangeHistory"
               ).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
