using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddIndexToEFMetaKeyAndClientId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                migrationBuilder.Sql("CREATE FULLTEXT INDEX IX_EFMeta_Key ON EFMeta ( `Key` );");
            }

            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_EFMeta_Key",
                    table: "EFMeta",
                    column: "Key");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFMeta_Key",
                table: "EFMeta");
        }
    }
}
