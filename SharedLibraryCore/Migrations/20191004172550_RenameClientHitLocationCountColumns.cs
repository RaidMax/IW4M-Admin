using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class RenameClientHitLocationCountColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM EFHitLocationCounts;

DROP TABLE EFHitLocationCounts;

CREATE TABLE EFHitLocationCounts (
    HitLocationCountId         INTEGER NOT NULL
                                       CONSTRAINT PK_EFHitLocationCounts PRIMARY KEY AUTOINCREMENT,
    Active                     INTEGER NOT NULL,
    EFClientStatisticsClientId INTEGER NOT NULL,
    HitCount                   INTEGER NOT NULL,
    HitOffsetAverage           REAL    NOT NULL,
    Location                   INTEGER NOT NULL,
    EFClientStatisticsServerId INTEGER NOT NULL,
    MaxAngleDistance           REAL    NOT NULL
                                       DEFAULT 0,
    CONSTRAINT FK_EFHitLocationCounts_EFClients_EFClientStatistics_ClientId FOREIGN KEY (
        EFClientStatisticsClientId
    )
    REFERENCES EFClients (ClientId) ON DELETE CASCADE,
    CONSTRAINT FK_EFHitLocationCounts_EFServers_EFClientStatistics_ServerId FOREIGN KEY (
        EFClientStatisticsServerId
    )
    REFERENCES EFServers (ServerId) ON DELETE CASCADE,
    CONSTRAINT FK_EFHitLocationCounts_EFClientStatistics_EFClientStatistics_ClientId_EFClientStatistics_ServerId FOREIGN KEY (
        EFClientStatisticsClientId,
        EFClientStatisticsServerId
    )
    REFERENCES EFClientStatistics (ClientId,
    ServerId) ON DELETE CASCADE
);

INSERT INTO EFHitLocationCounts (
                                    HitLocationCountId,
                                    Active,
                                    EFClientStatisticsClientId,
                                    HitCount,
                                    HitOffsetAverage,
                                    Location,
                                    EFClientStatisticsServerId,
                                    MaxAngleDistance
                                )
                                SELECT HitLocationCountId,
                                       Active,
                                       EFClientStatistics_ClientId,
                                       HitCount,
                                       HitOffsetAverage,
                                       Location,
                                       EFClientStatistics_ServerId,
                                       MaxAngleDistance
                                  FROM sqlitestudio_temp_table;

DROP TABLE sqlitestudio_temp_table;

CREATE INDEX IX_EFHitLocationCounts_EFClientStatistics_ServerId ON EFHitLocationCounts (
    EFClientStatisticsServerId
);

CREATE INDEX IX_EFHitLocationCounts_EFClientStatistics_ClientId_EFClientStatistics_ServerId ON EFHitLocationCounts (
    EFClientStatisticsClientId,
    EFClientStatisticsServerId
);

PRAGMA foreign_keys = 1;
", true);
            }

            else if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                migrationBuilder.Sql("ALTER TABLE `EFHitLocationCounts` CHANGE `EFClientStatistics_ClientId` `EFClientStatisticsClientId` INT(11) NOT NULL;");
                migrationBuilder.Sql("ALTER TABLE `EFHitLocationCounts` CHANGE `EFClientStatistics_ServerId` `EFClientStatisticsServerId` INT(11) NOT NULL;");
                migrationBuilder.Sql("CREATE INDEX `IX_EFClientStatisticsClientId_EFClientStatisticsServerId` ON `EFHitLocationCounts` (`EFClientStatisticsClientId`, `EFClientStatisticsServerId`);");
            }

            else
            {
                migrationBuilder.DropForeignKey(
                    name: "FK_EFHitLocationCounts_EFClients_EFClientStatistics_ClientId",
                    table: "EFHitLocationCounts");

                migrationBuilder.DropForeignKey(
                    name: "FK_EFHitLocationCounts_EFServers_EFClientStatistics_ServerId",
                    table: "EFHitLocationCounts");

                migrationBuilder.DropForeignKey(
                    name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatistics_ClientId_EFClientStatistics_ServerId",
                    table: "EFHitLocationCounts");

                migrationBuilder.RenameColumn(
                    name: "EFClientStatistics_ServerId",
                    table: "EFHitLocationCounts",
                    newName: "EFClientStatisticsServerId");

                migrationBuilder.RenameColumn(
                    name: "EFClientStatistics_ClientId",
                    table: "EFHitLocationCounts",
                    newName: "EFClientStatisticsClientId");

                migrationBuilder.RenameIndex(
                    name: "IX_EFHitLocationCounts_EFClientStatistics_ClientId_EFClientStatistics_ServerId",
                    table: "EFHitLocationCounts",
                    newName: "IX_EFHitLocationCounts_EFClientStatisticsClientId_EFClientStatisticsServerId");

                migrationBuilder.RenameIndex(
                    name: "IX_EFHitLocationCounts_EFClientStatistics_ServerId",
                    table: "EFHitLocationCounts",
                    newName: "IX_EFHitLocationCounts_EFClientStatisticsServerId");

                migrationBuilder.AddForeignKey(
                    name: "FK_EFHitLocationCounts_EFClients_EFClientStatisticsClientId",
                    table: "EFHitLocationCounts",
                    column: "EFClientStatisticsClientId",
                    principalTable: "EFClients",
                    principalColumn: "ClientId",
                    onDelete: ReferentialAction.Cascade);

                migrationBuilder.AddForeignKey(
                    name: "FK_EFHitLocationCounts_EFServers_EFClientStatisticsServerId",
                    table: "EFHitLocationCounts",
                    column: "EFClientStatisticsServerId",
                    principalTable: "EFServers",
                    principalColumn: "ServerId",
                    onDelete: ReferentialAction.Cascade);

                migrationBuilder.AddForeignKey(
                    name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatisticsClientId_EFClientStatisticsServerId",
                    table: "EFHitLocationCounts",
                    columns: new[] { "EFClientStatisticsClientId", "EFClientStatisticsServerId" },
                    principalTable: "EFClientStatistics",
                    principalColumns: new[] { "ClientId", "ServerId" },
                    onDelete: ReferentialAction.Cascade);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
