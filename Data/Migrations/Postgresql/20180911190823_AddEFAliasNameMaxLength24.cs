using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Postgresql
{
    public partial class AddEFAliasNameMaxLength24 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // hack: we can't alter the column on SQLite, but we need max length limit for the Index in MySQL etc
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.AlterColumn<string>(
                    name: "Name",
                    table: "EFAlias",
                    maxLength: 24,
                    nullable: false);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
