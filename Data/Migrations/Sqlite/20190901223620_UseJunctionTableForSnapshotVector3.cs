using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Data.Migrations.Sqlite
{
    public partial class UseJunctionTableForSnapshotVector3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.DropForeignKey(
                    name: "FK_Vector3_EFACSnapshot_EFACSnapshotSnapshotId",
                    table: "Vector3");

                migrationBuilder.DropIndex(
                    name: "IX_Vector3_EFACSnapshotSnapshotId",
                    table: "Vector3");

                migrationBuilder.DropColumn(
                    name: "EFACSnapshotSnapshotId",
                    table: "Vector3");
            }

            migrationBuilder.CreateTable(
                name: "EFACSnapshotVector3",
                columns: table => new
                {
                    ACSnapshotVector3Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SnapshotId = table.Column<int>(nullable: false),
                    Vector3Id = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EFACSnapshotVector3", x => x.ACSnapshotVector3Id);
                    table.ForeignKey(
                        name: "FK_EFACSnapshotVector3_EFACSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "EFACSnapshot",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EFACSnapshotVector3_Vector3_Vector3Id",
                        column: x => x.Vector3Id,
                        principalTable: "Vector3",
                        principalColumn: "Vector3Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshotVector3_SnapshotId",
                table: "EFACSnapshotVector3",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_EFACSnapshotVector3_Vector3Id",
                table: "EFACSnapshotVector3",
                column: "Vector3Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EFACSnapshotVector3");

            migrationBuilder.AddColumn<int>(
                name: "EFACSnapshotSnapshotId",
                table: "Vector3",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vector3_EFACSnapshotSnapshotId",
                table: "Vector3",
                column: "EFACSnapshotSnapshotId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vector3_EFACSnapshot_EFACSnapshotSnapshotId",
                table: "Vector3",
                column: "EFACSnapshotSnapshotId",
                principalTable: "EFACSnapshot",
                principalColumn: "SnapshotId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
