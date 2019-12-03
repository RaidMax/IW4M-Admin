using Microsoft.EntityFrameworkCore.Migrations;

namespace SharedLibraryCore.Migrations
{
    public partial class EnforceUniqueIndexForEFAliasIPName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS DUPLICATE_ALIASES;
                CREATE TABLE DUPLICATE_ALIASES AS
SELECT
    MIN(AliasId) MIN,
    MAX(AliasId) MAX,
    LinkId
FROM
    EFAlias
WHERE
    (IPAddress, NAME) IN(
    SELECT DISTINCT
        IPAddress,
        NAME
    FROM
        EFAlias
    GROUP BY
        EFAlias.IPAddress,
        NAME
    HAVING
        COUNT(IPAddress) > 1 AND COUNT(NAME) > 1
)
GROUP BY
    IPAddress
ORDER BY
    IPAddress;

                UPDATE
                    EFClients
SET CurrentAliasId = (SELECT MAX FROM DUPLICATE_ALIASES WHERE CurrentAliasId = MIN)
WHERE
    CurrentAliasId IN(
    SELECT
        MIN
    FROM
        DUPLICATE_ALIASES
);
                DELETE
                FROM
    EFAlias
WHERE
    AliasId IN(
    SELECT
        MIN
    FROM
        DUPLICATE_ALIASES
);

                DROP TABLE
    DUPLICATE_ALIASES;");
                return;
            }

            else if (migrationBuilder.ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                migrationBuilder.Sql(@"CREATE TABLE DUPLICATE_ALIASES
SELECT
    MIN(`AliasId`) `MIN`,
    MAX(`AliasId`) `MAX`,
    `LinkId`
FROM
    `EFAlias`
WHERE
    (`IPAddress`, `NAME`) IN(
    SELECT DISTINCT
        `IPAddress`,
        `NAME`
    FROM
        `EFAlias`
    GROUP BY
        `EFAlias`.`IPAddress`,
        `NAME`
    HAVING
        COUNT(`IPAddress`) > 1 AND COUNT(`NAME`) > 1
)
GROUP BY
    `IPAddress`
ORDER BY
    `IPAddress`;
SET
    SQL_SAFE_UPDATES = 0;
UPDATE
    `EFClients` AS `Client`
JOIN
    DUPLICATE_ALIASES `Duplicate`
ON
    `Client`.CurrentAliasId = `Duplicate`.`MIN`
SET
    `Client`.CurrentAliasId = `Duplicate`.`MAX`
WHERE
    `Client`.`CurrentAliasId` IN(
    SELECT
        `MIN`
    FROM
        DUPLICATE_ALIASES
);
DELETE
FROM
    `EFAlias`
WHERE
    `AliasId` IN(
    SELECT
        `MIN`
    FROM
        DUPLICATE_ALIASES
);
SET
    SQL_SAFE_UPDATES = 1;
DROP TABLE
    DUPLICATE_ALIASES;");
            }

            else
            {
                migrationBuilder.Sql(@"CREATE TEMPORARY TABLE DUPLICATE_ALIASES AS
SELECT
    MIN(""AliasId"") ""MIN"",
    MAX(""AliasId"") ""MAX"",
    MIN(""LinkId"") ""LinkId""
FROM
    ""EFAlias""
WHERE
    (""IPAddress"", ""Name"") IN(
    SELECT DISTINCT
        ""IPAddress"",
        ""Name""
    FROM
        ""EFAlias""
    GROUP BY
        ""EFAlias"".""IPAddress"",
        ""Name""
    HAVING
        COUNT(""IPAddress"") > 1 AND COUNT(""Name"") > 1
)
GROUP BY
    ""IPAddress""
ORDER BY
    ""IPAddress"";
UPDATE
    ""EFClients"" AS ""Client""
SET
    ""CurrentAliasId"" = ""Duplicate"".""MAX""
FROM
    DUPLICATE_ALIASES ""Duplicate""
WHERE
    ""Client"".""CurrentAliasId"" IN(
    SELECT
        ""MIN""
    FROM
        DUPLICATE_ALIASES
)
AND
	""Client"".""CurrentAliasId"" = ""Duplicate"".""MIN"";
DELETE
FROM
    ""EFAlias""
WHERE
    ""AliasId"" IN(
    SELECT
        ""MIN""
    FROM
        DUPLICATE_ALIASES
);
DROP TABLE
    DUPLICATE_ALIASES;");
            }

            migrationBuilder.CreateIndex(
                    name: "IX_EFAlias_Name_IPAddress",
                    table: "EFAlias",
                    columns: new[] { "Name", "IPAddress" },
                    unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
