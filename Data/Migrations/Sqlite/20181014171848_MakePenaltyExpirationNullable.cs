using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class MakePenaltyExpirationNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM EFPenalties;

DROP TABLE EFPenalties;

CREATE TABLE EFPenalties (
    PenaltyId        INTEGER NOT NULL
                             CONSTRAINT PK_EFPenalties PRIMARY KEY AUTOINCREMENT,
    Active           INTEGER NOT NULL,
    Expires          TEXT,
    LinkId           INTEGER NOT NULL,
    OffenderId       INTEGER NOT NULL,
    Offense          TEXT    NOT NULL,
    PunisherId       INTEGER NOT NULL,
    Type             INTEGER NOT NULL,
    [When]           TEXT    NOT NULL,
    AutomatedOffense TEXT,
    CONSTRAINT FK_EFPenalties_EFAliasLinks_LinkId FOREIGN KEY (
        LinkId
    )
    REFERENCES EFAliasLinks (AliasLinkId) ON DELETE CASCADE,
    CONSTRAINT FK_EFPenalties_EFClients_OffenderId FOREIGN KEY (
        OffenderId
    )
    REFERENCES EFClients (ClientId) ON DELETE RESTRICT,
    CONSTRAINT FK_EFPenalties_EFClients_PunisherId FOREIGN KEY (
        PunisherId
    )
    REFERENCES EFClients (ClientId) ON DELETE RESTRICT
);

INSERT INTO EFPenalties (
                            PenaltyId,
                            Active,
                            Expires,
                            LinkId,
                            OffenderId,
                            Offense,
                            PunisherId,
                            Type,
                            [When],
                            AutomatedOffense
                        )
                        SELECT PenaltyId,
                               Active,
                               Expires,
                               LinkId,
                               OffenderId,
                               Offense,
                               PunisherId,
                               Type,
                               ""When"",
                               AutomatedOffense
                          FROM sqlitestudio_temp_table;

                DROP TABLE sqlitestudio_temp_table;

                CREATE INDEX IX_EFPenalties_LinkId ON EFPenalties(
                    ""LinkId""
                );

                CREATE INDEX IX_EFPenalties_OffenderId ON EFPenalties(
                    ""OffenderId""
                );

                CREATE INDEX IX_EFPenalties_PunisherId ON EFPenalties(
                    ""PunisherId""
                );

                PRAGMA foreign_keys = 1; ");
            }
            else
            {
                migrationBuilder.AlterColumn<DateTime>(
                    name: "Expires",
                    table: "EFPenalties",
                    nullable: true,
                    oldClrType: typeof(DateTime));
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Expires",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldNullable: true);
        }
    }
}
