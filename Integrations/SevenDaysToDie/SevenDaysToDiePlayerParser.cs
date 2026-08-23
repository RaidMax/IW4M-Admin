using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Data.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Integrations.SevenDaysToDie;

public static partial class SevenDaysToDiePlayerParser
{
    public static IReadOnlyList<SevenDaysToDiePlayer> Parse(string response)
    {
        var players = new List<SevenDaysToDiePlayer>();

        foreach (var line in response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var playerMatch = PlayerLineRegex().Match(line);
            if (!playerMatch.Success)
            {
                continue;
            }

            var fields = new Dictionary<string, string>();
            foreach (Match fieldMatch in FieldRegex().Matches(line))
            {
                fields[fieldMatch.Groups["key"].Value.ToLowerInvariant()] =
                    fieldMatch.Groups["value"].Value.Trim();
            }

            var name = playerMatch.Groups["name"].Value.Trim();
            if (fields.TryGetValue("name", out var explicitName) ||
                fields.TryGetValue("playername", out explicitName))
            {
                name = explicitName.Trim(' ', '\'', '"');
            }

            var entityId = ParseInteger(playerMatch.Groups["entity"].Value);
            var platformId = GetValue(fields, "steamid") ?? GetValue(fields, "pltfmid") ??
                GetValue(fields, "crossid") ?? entityId.ToString(CultureInfo.InvariantCulture);
            var address = NormalizeAddress(GetValue(fields, "ip"));
            var position = ParseVector(line, "pos");
            var rotation = ParseVector(line, "rot");

            players.Add(new SevenDaysToDiePlayer
            {
                ClientNumber = ParseInteger(playerMatch.Groups["slot"].Value),
                EntityId = entityId,
                NetworkId = CreateNetworkId(platformId),
                CurrentAlias = new EFAlias
                {
                    Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name,
                    IPAddress = address.ConvertToIP()
                },
                Ping = Math.Clamp(ParseInteger(GetValue(fields, "ping"), 999), 0, 999),
                Score = Math.Max(0, ParseInteger(GetValue(fields, "level"))),
                ZombieKills = Math.Max(0, ParseInteger(GetValue(fields, "zombies"))),
                PlayerDeaths = Math.Max(0, ParseInteger(GetValue(fields, "deaths"))),
                Position = position,
                Rotation = rotation,
                Health = Math.Clamp(ParseInteger(GetValue(fields, "health"), 100), 0, 100)
            });
        }

        return players;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static int ParseInteger(string value, int fallback = 0) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? (int)parsed
            : fallback;

    private static Vector3 ParseVector(string line, string field)
    {
        var match = Regex.Match(line,
            $@"(?:^|,\s*){Regex.Escape(field)}=\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)",
            RegexOptions.IgnoreCase);

        return match.Success
            ? new Vector3(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value),
                ParseFloat(match.Groups[3].Value))
            : new Vector3();
    }

    private static float ParseFloat(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string NormalizeAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0.0";
        }

        value = value.Trim().Trim('[', ']');
        var portSeparator = value.LastIndexOf(':');
        if (portSeparator > -1 && value.Count(character => character == ':') == 1)
        {
            value = value[..portSeparator];
        }

        return System.Net.IPAddress.TryParse(value, out var address) &&
               address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? value
            : "0.0.0.0";
    }

    private static long CreateNetworkId(string platformId)
    {
        var numericId = NumericPlatformIdRegex().Match(platformId ?? string.Empty).Value;
        if (long.TryParse(numericId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(platformId ?? string.Empty));
        var generated = BitConverter.ToInt64(digest, 0) & long.MaxValue;
        return generated == 0 ? 1 : generated;
    }

    [GeneratedRegex(@"^\s*(?<slot>\d+)\.\s+id=(?<entity>\d+),\s*(?<name>.*?)\s*,\s*pos=",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLineRegex();

    [GeneratedRegex(@"(?:^|,\s*)(?<key>[A-Za-z]+)=(?<value>[^,\r\n]*)")]
    private static partial Regex FieldRegex();

    [GeneratedRegex(@"\d{15,19}")]
    private static partial Regex NumericPlatformIdRegex();
}
