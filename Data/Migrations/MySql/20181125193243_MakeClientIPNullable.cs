using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class MakeClientIPNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM EFAlias;

DROP TABLE EFAlias;

CREATE TABLE EFAlias (
    AliasId   INTEGER NOT NULL
                      CONSTRAINT PK_EFAlias PRIMARY KEY AUTOINCREMENT,
    Active    INTEGER NOT NULL,
    DateAdded TEXT    NOT NULL,
    IPAddress INTEGER,
    LinkId    INTEGER NOT NULL,
    Name      TEXT    NOT NULL,
    CONSTRAINT FK_EFAlias_EFAliasLinks_LinkId FOREIGN KEY (
        LinkId
    )
    REFERENCES EFAliasLinks (AliasLinkId) ON DELETE RESTRICT
);

INSERT INTO EFAlias (
                        AliasId,
                        Active,
                        DateAdded,
                        IPAddress,
                        LinkId,
                        Name
                    )
                    SELECT AliasId,
                           Active,
                           DateAdded,
                           IPAddress,
                           LinkId,
                           Name
                      FROM sqlitestudio_temp_table;

DROP TABLE sqlitestudio_temp_table;

CREATE INDEX IX_EFAlias_LinkId ON EFAlias (
    ""LinkId""
);

                CREATE INDEX IX_EFAlias_IPAddress ON EFAlias(
                    ""IPAddress""
                );

                CREATE INDEX IX_EFAlias_Name ON EFAlias(
                    ""Name""
                );

                PRAGMA foreign_keys = 1;
                ", suppressTransaction:true);
            }
            else
            {
                migrationBuilder.AlterColumn<int>(
                    name: "IPAddress",
                    table: "EFAlias",
                    nullable: true,
                    oldClrType: typeof(int));
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "IPAddress",
                table: "EFAlias",
                nullable: false,
                oldClrType: typeof(int),
                oldNullable: true);
        }
    }
}
