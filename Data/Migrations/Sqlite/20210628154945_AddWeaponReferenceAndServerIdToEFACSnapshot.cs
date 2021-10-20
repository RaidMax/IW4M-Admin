using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class AddWeaponReferenceAndServerIdToEFACSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                          FROM EFACSnapshot;

DROP TABLE EFACSnapshot;

CREATE TABLE EFACSnapshot (
    Active                  INTEGER NOT NULL,
    TimeSinceLastEvent      INTEGER NOT NULL,
    SnapshotId              INTEGER NOT NULL
                                    CONSTRAINT PK_EFACSnapshot PRIMARY KEY AUTOINCREMENT,
    ClientId                INTEGER NOT NULL,
    ServerId                INTEGER CONSTRAINT FK_EFACSnapshot_EFServers_ServerId REFERENCES EFServers (ServerId) ON DELETE RESTRICT,
    [When]                  TEXT    NOT NULL,
    CurrentSessionLength    INTEGER NOT NULL,
    EloRating               REAL    NOT NULL,
    SessionScore            INTEGER NOT NULL,
    SessionSPM              REAL    NOT NULL,
    Hits                    INTEGER NOT NULL,
    Kills                   INTEGER NOT NULL,
    Deaths                  INTEGER NOT NULL,
    CurrentStrain           REAL    NOT NULL,
    StrainAngleBetween      REAL    NOT NULL,
    SessionAngleOffset      REAL    NOT NULL,
    LastStrainAngleId       INTEGER NOT NULL,
    HitOriginId             INTEGER NOT NULL,
    HitDestinationId        INTEGER NOT NULL,
    Distance                REAL    NOT NULL,
    CurrentViewAngleId      INTEGER,
    WeaponId                INTEGER NOT NULL,
    WeaponReference         TEXT,
    HitLocation             INTEGER NOT NULL,
    HitType                 INTEGER NOT NULL,
    RecoilOffset            REAL    NOT NULL
                                    DEFAULT 0.0,
    SessionAverageSnapValue REAL    NOT NULL
                                    DEFAULT 0.0,
    SessionSnapHits         INTEGER NOT NULL
                                    DEFAULT 0,
    CONSTRAINT FK_EFACSnapshot_EFClients_ClientId FOREIGN KEY (
        ClientId
    )
    REFERENCES EFClients (ClientId) ON DELETE CASCADE,
    CONSTRAINT FK_EFACSnapshot_Vector3_CurrentViewAngleId FOREIGN KEY (
        CurrentViewAngleId
    )
    REFERENCES Vector3 (Vector3Id) ON DELETE RESTRICT,
    CONSTRAINT FK_EFACSnapshot_Vector3_HitDestinationId FOREIGN KEY (
        HitDestinationId
    )
    REFERENCES Vector3 (Vector3Id) ON DELETE CASCADE,
    CONSTRAINT FK_EFACSnapshot_Vector3_HitOriginId FOREIGN KEY (
        HitOriginId
    )
    REFERENCES Vector3 (Vector3Id) ON DELETE CASCADE,
    CONSTRAINT FK_EFACSnapshot_Vector3_LastStrainAngleId FOREIGN KEY (
        LastStrainAngleId
    )
    REFERENCES Vector3 (Vector3Id) ON DELETE CASCADE
);

INSERT INTO EFACSnapshot (
                             Active,
                             TimeSinceLastEvent,
                             SnapshotId,
                             ClientId,
                             [When],
                             CurrentSessionLength,
                             EloRating,
                             SessionScore,
                             SessionSPM,
                             Hits,
                             Kills,
                             Deaths,
                             CurrentStrain,
                             StrainAngleBetween,
                             SessionAngleOffset,
                             LastStrainAngleId,
                             HitOriginId,
                             HitDestinationId,
                             Distance,
                             CurrentViewAngleId,
                             WeaponId,
                             HitLocation,
                             HitType,
                             RecoilOffset,
                             SessionAverageSnapValue,
                             SessionSnapHits
                         )
                         SELECT Active,
                                TimeSinceLastEvent,
                                SnapshotId,
                                ClientId,
                                ""When"",
                                CurrentSessionLength,
                                EloRating,
                                SessionScore,
                                SessionSPM,
                                Hits,
                                Kills,
                                Deaths,
                                CurrentStrain,
                                StrainAngleBetween,
                                SessionAngleOffset,
                                LastStrainAngleId,
                                HitOriginId,
                                HitDestinationId,
                                Distance,
                                CurrentViewAngleId,
                                WeaponId,
                                HitLocation,
                                HitType,
                                RecoilOffset,
                                SessionAverageSnapValue,
                                SessionSnapHits
                           FROM sqlitestudio_temp_table;

DROP TABLE sqlitestudio_temp_table;

CREATE INDEX IX_EFACSnapshot_ClientId ON EFACSnapshot (
    ""ClientId""
);

CREATE INDEX IX_EFACSnapshot_CurrentViewAngleId ON EFACSnapshot (
    ""CurrentViewAngleId""
);

CREATE INDEX IX_EFACSnapshot_HitDestinationId ON EFACSnapshot (
    ""HitDestinationId""
);

CREATE INDEX IX_EFACSnapshot_HitOriginId ON EFACSnapshot (
    ""HitOriginId""
);

CREATE INDEX IX_EFACSnapshot_LastStrainAngleId ON EFACSnapshot (
    ""LastStrainAngleId""
);

CREATE INDEX IX_EFACSnapshot_ServerId ON EFACSnapshot (
    ""_ServerId""
);


PRAGMA foreign_keys = 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
