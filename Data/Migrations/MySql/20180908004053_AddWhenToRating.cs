using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class AddWhenToRating : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "When",
                table: "EFRating",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "When",
                table: "EFRating");
        }
    }
}
