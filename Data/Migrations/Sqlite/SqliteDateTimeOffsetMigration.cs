using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq;

namespace Data.Migrations.Sqlite;

// EF's binary converter stores local ticks / 1000 in the high bits and a signed
// eleven-bit offset (minutes) in the low bits. SQLite's table rebuild does not
// convert the old ISO text values into that representation.
internal static class SqliteDateTimeOffsetMigration
{
    private static readonly (string Table, string[] Columns)[] DateColumns =
    [
        ("EFZombieRoundClientStats", ["StartTime", "EndTime"]),
        ("EFZombieMatches", ["CreatedDateTime", "UpdatedDateTime", "MatchStartDate", "MatchEndDate"]),
        ("EFZombieEvents", ["CreatedDateTime", "UpdatedDateTime"]),
        ("EFZombieClientStats", ["CreatedDateTime", "UpdatedDateTime"]),
        ("EFZombieClientStatRecords", ["CreatedDateTime", "UpdatedDateTime"]),
        ("EFClientStatTagValues", ["CreatedDateTime", "UpdatedDateTime"]),
        ("EFClientStatTags", ["CreatedDateTime", "UpdatedDateTime"])
    ];

    internal static void EncodeText(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, columns) in DateColumns)
        foreach (var column in columns)
        {
            var v = $"\"{column}\"";
            var offsetLength = $"(CASE WHEN substr({v}, -1) = 'Z' THEN 1 ELSE 6 END)";
            var offset = $"(CASE WHEN substr({v}, -1) = 'Z' THEN 0 ELSE " +
                         $"(CASE WHEN substr({v}, -6, 1) = '-' THEN -1 ELSE 1 END) * " +
                         $"(CAST(substr({v}, -5, 2) AS INTEGER) * 60 + CAST(substr({v}, -2) AS INTEGER)) END)";
            // Compute days separately to avoid floating-point loss of fractional seconds.
            var ticks = $"(CAST(julianday(substr({v}, 1, 10)) - 1721425.5 AS INTEGER) * 864000000000 + " +
                        $"CAST(substr({v}, 12, 2) AS INTEGER) * 36000000000 + " +
                        $"CAST(substr({v}, 15, 2) AS INTEGER) * 600000000 + " +
                        $"CAST(substr({v}, 18, 2) AS INTEGER) * 10000000 + " +
                        $"CASE WHEN substr({v}, 20, 1) = '.' THEN " +
                        $"CAST(substr(substr({v}, 21, length({v}) - 20 - {offsetLength}) || '0000000', 1, 7) AS INTEGER) ELSE 0 END)";
            migrationBuilder.Sql($"UPDATE \"{table}\" SET {v} = (({ticks} / 1000) << 11) | ({offset} & 2047) " +
                                 $"WHERE typeof({v}) = 'text' AND substr({v}, 5, 1) = '-';");
        }
    }

    internal static void DecodeBinary(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, columns) in DateColumns)
        foreach (var column in columns)
        {
            var v = $"\"{column}\"";
            var ticks = $"(({v} >> 11) * 1000)";
            var offset = $"(CASE WHEN ({v} & 2047) >= 1024 THEN ({v} & 2047) - 2048 ELSE ({v} & 2047) END)";
            migrationBuilder.Sql($"UPDATE \"{table}\" SET {v} = " +
                $"strftime('%Y-%m-%d %H:%M:%S', ({ticks} / 10000000) - 62135596800, 'unixepoch') || " +
                $"printf('.%07d%s%02d:%02d', {ticks} % 10000000, CASE WHEN {offset} < 0 THEN '-' ELSE '+' END, abs({offset}) / 60, abs({offset}) % 60) " +
                $"WHERE typeof({v}) = 'integer';");
        }
    }

    internal static void NormalizeUtc(MigrationBuilder migrationBuilder)
    {
        // EasterEggOccurredAt was introduced after the historical text conversion.
        foreach (var (table, columns) in DateColumns.Append(("EFZombieMatches", new[] { "EasterEggOccurredAt" })))
        foreach (var column in columns)
        {
            var v = $"\"{column}\"";
            var offset = $"(CASE WHEN ({v} & 2047) >= 1024 THEN ({v} & 2047) - 2048 ELSE ({v} & 2047) END)";
            // High bits hold local ticks / 1000. One offset minute is 600000 units.
            // Subtract the offset and clear the low bits to encode the same UTC instant.
            migrationBuilder.Sql($"UPDATE \"{table}\" SET {v} = ((({v} >> 11) - {offset} * 600000) << 11) " +
                                 $"WHERE typeof({v}) = 'integer' AND ({v} & 2047) != 0;");
        }
    }
}
