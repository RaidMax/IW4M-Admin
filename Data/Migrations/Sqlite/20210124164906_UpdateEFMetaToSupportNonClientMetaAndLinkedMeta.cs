using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.Sqlite
{
    public partial class UpdateEFMetaToSupportNonClientMetaAndLinkedMeta : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

            CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                                      FROM EFMeta;

            DROP TABLE EFMeta;

            CREATE TABLE EFMeta (
                MetaId       INTEGER NOT NULL
                                     CONSTRAINT PK_EFMeta PRIMARY KEY AUTOINCREMENT,
                Active       INTEGER NOT NULL,
                ClientId     INTEGER,
                Created      TEXT    NOT NULL,
                Extra        TEXT,
                [Key]        TEXT    NOT NULL,
                Updated      TEXT    NOT NULL,
                Value        TEXT    NOT NULL,
                LinkedMetaId INTEGER CONSTRAINT IX_EFMeta_LinkedMetaId REFERENCES EFMeta (MetaId) ON DELETE SET NULL,
                CONSTRAINT FK_EFMeta_EFClients_ClientId FOREIGN KEY (
                    ClientId
                )
                REFERENCES EFClients (ClientId) ON DELETE CASCADE
            );

            INSERT INTO EFMeta (
                       MetaId,
                       Active,
                       ClientId,
                       Created,
                       Extra,
                       [Key],
                       Updated,
                       Value
                   )
                   SELECT MetaId,
                          Active,
                          ClientId,
                          Created,
                          Extra,
                          ""Key"",
                          Updated,
                          Value
                     FROM sqlitestudio_temp_table;

            DROP TABLE sqlitestudio_temp_table;

            CREATE INDEX IX_EFMeta_ClientId ON EFMeta(
                ""ClientId""
            );

            CREATE INDEX IX_EFMeta_Key ON EFMeta (
                ""Key""
            );

            CREATE INDEX IX_EFMeta_LinkedMetaId ON EFMeta (
                LinkedMetaId
            );

            PRAGMA foreign_keys = 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"PRAGMA foreign_keys = 0;

            CREATE TABLE sqlitestudio_temp_table AS SELECT *
                                                      FROM EFMeta;

            DROP TABLE EFMeta;

            CREATE TABLE EFMeta (
                MetaId   INTEGER NOT NULL
                                 CONSTRAINT PK_EFMeta PRIMARY KEY AUTOINCREMENT,
                Active   INTEGER NOT NULL,
                ClientId INTEGER NOT NULL,
                Created  TEXT    NOT NULL,
                Extra    TEXT,
                [Key]    TEXT    NOT NULL,
                Updated  TEXT    NOT NULL,
                Value    TEXT    NOT NULL,
                CONSTRAINT FK_EFMeta_EFClients_ClientId FOREIGN KEY (
                    ClientId
                )
                REFERENCES EFClients (ClientId) ON DELETE CASCADE
            );

            INSERT INTO EFMeta (
                       MetaId,
                       Active,
                       ClientId,
                       Created,
                       Extra,
                       [Key],
                       Updated,
                       Value
                   )
                   SELECT MetaId,
                          Active,
                          ClientId,
                          Created,
                          Extra,
                          ""Key"",
                          Updated,
                          Value
                     FROM sqlitestudio_temp_table;

            DROP TABLE sqlitestudio_temp_table;

            CREATE INDEX IX_EFMeta_ClientId ON EFMeta(
                ""ClientId""
            );

            CREATE INDEX IX_EFMeta_Key ON EFMeta(
                ""Key""
            );

            PRAGMA foreign_keys = 1;");
        }
    }
}
