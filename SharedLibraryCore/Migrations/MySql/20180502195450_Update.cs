using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Migrations.MySql
{
    public partial class Update : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "MaxAngleDistance",
                table: "EFHitLocationCounts",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<double>(
                name: "MaxStrain",
                table: "EFClientStatistics",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAngleDistance",
                table: "EFHitLocationCounts");

            migrationBuilder.DropColumn(
                name: "MaxStrain",
                table: "EFClientStatistics");
        }
    }
}
