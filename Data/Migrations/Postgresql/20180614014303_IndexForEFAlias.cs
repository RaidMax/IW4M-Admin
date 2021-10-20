using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace Data.Migrations.Postgresql
{
    public partial class IndexForEFAlias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EFAlias_IPAddress",
                table: "EFAlias",
                column: "IPAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EFAlias_IPAddress",
                table: "EFAlias");
        }
    }
}
