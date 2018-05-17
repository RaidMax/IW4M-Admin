using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations
{
    public partial class AddRollingKDR : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "RollingWeightedKDR",
                table: "EFClientStatistics",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RollingWeightedKDR",
                table: "EFClientStatistics");
        }
    }
}
