using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations.Sqlite
{
    public partial class SetCaseSensitiveCoallationForAliasNameMySQL : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                // this prevents duplicate aliases from being added by changing it to case sensitive collation
                migrationBuilder.Sql(@"ALTER TABLE `EFAlias` MODIFY
                                `Name` VARCHAR(24) 
                                CHARACTER SET utf8
                                COLLATE utf8_bin;");
            };
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
