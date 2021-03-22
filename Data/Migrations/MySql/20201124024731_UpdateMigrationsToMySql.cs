using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations.MySql
{
    public partial class UpdateMigrationsToMySql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("set foreign_key_checks=0;");

            migrationBuilder.AlterColumn<float>(
                name: "Z",
                table: "Vector3",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<float>(
                name: "Y",
                table: "Vector3",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<float>(
                name: "X",
                table: "Vector3",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Vector3Id",
                table: "Vector3",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<long>(
                name: "TotalPlayTime",
                table: "EFServerStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "TotalKills",
                table: "EFServerStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFServerStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFServerStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "StatisticId",
                table: "EFServerStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Port",
                table: "EFServers",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPasswordProtected",
                table: "EFServers",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "HostName",
                table: "EFServers",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GameName",
                table: "EFServers",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EndPoint",
                table: "EFServers",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFServers",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFServers",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTime>(
                name: "When",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFRating",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RatingHistoryId",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Ranking",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "Performance",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<bool>(
                name: "Newest",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ActivityAmount",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "RatingId",
                table: "EFRating",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<DateTime>(
                name: "When",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PunisherId",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Offense",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "OffenderId",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEvadedOffense",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Expires",
                table: "EFPenalties",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AutomatedOffense",
                table: "EFPenalties",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "PenaltyId",
                table: "EFPenalties",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Updated",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "EFMeta",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Extra",
                table: "EFMeta",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Created",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "MetaId",
                table: "EFMeta",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<float>(
                name: "MaxAngleDistance",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Location",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<float>(
                name: "HitOffsetAverage",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "HitCount",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "EFClientStatisticsServerId",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "EFClientStatisticsClientId",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitLocationCountId",
                table: "EFHitLocationCounts",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<double>(
                name: "VisionAverage",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "TimePlayed",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SnapHitCount",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "Skill",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "SPM",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "RollingWeightedKDR",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "MaxStrain",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Kills",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "EloRating",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Deaths",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "AverageSnapValue",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "AverageRecoilOffset",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientStatistics",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "TotalConnectionTime",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordSalt",
                table: "EFClients",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "EFClients",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "NetworkId",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Masked",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastConnection",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FirstConnection",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentAliasId",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Connections",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AliasLinkId",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClients",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientRatingHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFClientRatingHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "RatingHistoryId",
                table: "EFClientRatingHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeSent",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "SentIngame",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EFClientMessages",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "MessageId",
                table: "EFClientMessages",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<DateTime>(
                name: "When",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Weapon",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "VisibilityPercentage",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "ViewAnglesVector3Id",
                table: "EFClientKills",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "VictimId",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "ServerId",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Map",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "KillOriginVector3Id",
                table: "EFClientKills",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsKill",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitLoc",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "Fraction",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "DeathType",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "DeathOriginVector3Id",
                table: "EFClientKills",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Damage",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AttackerId",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "KillId",
                table: "EFClientKills",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "TypeOfChange",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeChanged",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "TargetEntityId",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "PreviousValue",
                table: "EFChangeHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OriginEntityId",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ImpersonationEntityId",
                table: "EFChangeHistory",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentValue",
                table: "EFChangeHistory",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "EFChangeHistory",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ChangeHistoryId",
                table: "EFChangeHistory",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFAliasLinks",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AliasLinkId",
                table: "EFAliasLinks",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "SearchableName",
                table: "EFAlias",
                maxLength: 24,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 24,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EFAlias",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFAlias",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "IPAddress",
                table: "EFAlias",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateAdded",
                table: "EFAlias",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFAlias",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AliasId",
                table: "EFAlias",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Vector3Id",
                table: "EFACSnapshotVector3",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SnapshotId",
                table: "EFACSnapshotVector3",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFACSnapshotVector3",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ACSnapshotVector3Id",
                table: "EFACSnapshotVector3",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<DateTime>(
                name: "When",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "WeaponId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "TimeSinceLastEvent",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "StrainAngleBetween",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "SessionSnapHits",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SessionScore",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "SessionSPM",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "SessionAverageSnapValue",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "SessionAngleOffset",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "RecoilOffset",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "LastStrainAngleId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Kills",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Hits",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitType",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitOriginId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitLocation",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "HitDestinationId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "EloRating",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "Distance",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "Deaths",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentViewAngleId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "CurrentStrain",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentSessionLength",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SnapshotId",
                table: "EFACSnapshot",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddForeignKey(
                name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatisticsCli~",
                table: "EFHitLocationCounts",
                columns: new[] { "EFClientStatisticsClientId", "EFClientStatisticsServerId" },
                principalTable: "EFClientStatistics",
                principalColumns: new[] { "ClientId", "ServerId" },
                onDelete: ReferentialAction.Cascade);
            
            migrationBuilder.Sql("set foreign_key_checks=1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatisticsCli~",
                table: "EFHitLocationCounts");

            migrationBuilder.RenameIndex(
                name: "IX_EFHitLocationCounts_EFClientStatisticsClientId_EFClientStati~",
                table: "EFHitLocationCounts",
                newName: "IX_EFHitLocationCounts_EFClientStatisticsClientId_EFClientStatisticsServerId");

            migrationBuilder.AlterColumn<double>(
                name: "Z",
                table: "Vector3",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(float));

            migrationBuilder.AlterColumn<double>(
                name: "Y",
                table: "Vector3",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(float));

            migrationBuilder.AlterColumn<double>(
                name: "X",
                table: "Vector3",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(float));

            migrationBuilder.AlterColumn<int>(
                name: "Vector3Id",
                table: "Vector3",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "TotalPlayTime",
                table: "EFServerStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "TotalKills",
                table: "EFServerStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFServerStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFServerStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "StatisticId",
                table: "EFServerStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Port",
                table: "EFServers",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "IsPasswordProtected",
                table: "EFServers",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<string>(
                name: "HostName",
                table: "EFServers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GameName",
                table: "EFServers",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EndPoint",
                table: "EFServers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFServers",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFServers",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<string>(
                name: "When",
                table: "EFRating",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFRating",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RatingHistoryId",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Ranking",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "Performance",
                table: "EFRating",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "Newest",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ActivityAmount",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "RatingId",
                table: "EFRating",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "When",
                table: "EFPenalties",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "PunisherId",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<string>(
                name: "Offense",
                table: "EFPenalties",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<int>(
                name: "OffenderId",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "IsEvadedOffense",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<string>(
                name: "Expires",
                table: "EFPenalties",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AutomatedOffense",
                table: "EFPenalties",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "PenaltyId",
                table: "EFPenalties",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "EFMeta",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string));

            migrationBuilder.AlterColumn<string>(
                name: "Updated",
                table: "EFMeta",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "EFMeta",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Extra",
                table: "EFMeta",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Created",
                table: "EFMeta",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFMeta",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFMeta",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "MetaId",
                table: "EFMeta",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<double>(
                name: "MaxAngleDistance",
                table: "EFHitLocationCounts",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(float));

            migrationBuilder.AlterColumn<int>(
                name: "Location",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "HitOffsetAverage",
                table: "EFHitLocationCounts",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(float));

            migrationBuilder.AlterColumn<int>(
                name: "HitCount",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "EFClientStatisticsServerId",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "EFClientStatisticsClientId",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "HitLocationCountId",
                table: "EFHitLocationCounts",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<double>(
                name: "VisionAverage",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "TimePlayed",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "SnapHitCount",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "Skill",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "SPM",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "RollingWeightedKDR",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "MaxStrain",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "Kills",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "EloRating",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "Deaths",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "AverageSnapValue",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "AverageRecoilOffset",
                table: "EFClientStatistics",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientStatistics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "TotalConnectionTime",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<string>(
                name: "PasswordSalt",
                table: "EFClients",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "EFClients",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NetworkId",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "Masked",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<string>(
                name: "LastConnection",
                table: "EFClients",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<string>(
                name: "FirstConnection",
                table: "EFClients",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "CurrentAliasId",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Connections",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "AliasLinkId",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClients",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientRatingHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFClientRatingHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "RatingHistoryId",
                table: "EFClientRatingHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "TimeSent",
                table: "EFClientMessages",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFClientMessages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "SentIngame",
                table: "EFClientMessages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EFClientMessages",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFClientMessages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFClientMessages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "EFClientMessages",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "When",
                table: "EFClientKills",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "Weapon",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "VisibilityPercentage",
                table: "EFClientKills",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "ViewAnglesVector3Id",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "VictimId",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<int>(
                name: "Map",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "KillOriginVector3Id",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IsKill",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "HitLoc",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "Fraction",
                table: "EFClientKills",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "DeathType",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "DeathOriginVector3Id",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Damage",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "AttackerId",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "KillId",
                table: "EFClientKills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "TypeOfChange",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<string>(
                name: "TimeChanged",
                table: "EFChangeHistory",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "TargetEntityId",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<string>(
                name: "PreviousValue",
                table: "EFChangeHistory",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OriginEntityId",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "ImpersonationEntityId",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentValue",
                table: "EFChangeHistory",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "EFChangeHistory",
                type: "TEXT",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ChangeHistoryId",
                table: "EFChangeHistory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFAliasLinks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "AliasLinkId",
                table: "EFAliasLinks",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "SearchableName",
                table: "EFAlias",
                type: "TEXT",
                maxLength: 24,
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 24,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EFAlias",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<int>(
                name: "LinkId",
                table: "EFAlias",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "IPAddress",
                table: "EFAlias",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DateAdded",
                table: "EFAlias",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFAlias",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "AliasId",
                table: "EFAlias",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Vector3Id",
                table: "EFACSnapshotVector3",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "SnapshotId",
                table: "EFACSnapshotVector3",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFACSnapshotVector3",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "ACSnapshotVector3Id",
                table: "EFACSnapshotVector3",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "When",
                table: "EFACSnapshot",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<int>(
                name: "WeaponId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "TimeSinceLastEvent",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "StrainAngleBetween",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "SessionSnapHits",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "SessionScore",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "SessionSPM",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "SessionAverageSnapValue",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "SessionAngleOffset",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "RecoilOffset",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "LastStrainAngleId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Kills",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Hits",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "HitType",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "HitOriginId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "HitLocation",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "HitDestinationId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "EloRating",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<double>(
                name: "Distance",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "Deaths",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "CurrentViewAngleId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<double>(
                name: "CurrentStrain",
                table: "EFACSnapshot",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(double));

            migrationBuilder.AlterColumn<int>(
                name: "CurrentSessionLength",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<int>(
                name: "Active",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<int>(
                name: "SnapshotId",
                table: "EFACSnapshot",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddForeignKey(
                name: "FK_EFHitLocationCounts_EFClientStatistics_EFClientStatisticsClientId_EFClientStatisticsServerId",
                table: "EFHitLocationCounts",
                columns: new[] { "EFClientStatisticsClientId", "EFClientStatisticsServerId" },
                principalTable: "EFClientStatistics",
                principalColumns: new[] { "ClientId", "ServerId" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
