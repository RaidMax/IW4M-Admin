using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddTwoFactorSecretToEFClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecret",
                table: "EFClients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoFactorSecret",
                table: "EFClients");
        }
    }
}
