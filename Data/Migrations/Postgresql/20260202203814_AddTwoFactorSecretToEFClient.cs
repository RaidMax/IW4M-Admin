using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
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
                type: "text",
                nullable: true);
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
