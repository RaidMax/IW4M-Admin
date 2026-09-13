using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Integrations.SevenDaysToDie;
using SharedLibraryCore;

namespace IW4MAdmin.Plugins.LiveRadar.Services;

public sealed class SevenDaysToDieRadarService(IHttpClientFactory httpClientFactory)
{
    private const int MaxTileBytes = 4 * 1024 * 1024;
    private const int MaxSecretBytes = 4 * 1024;
    private static readonly TimeSpan MapLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PlayerLifetime = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public async Task<object> GetMapAsync(Server server, CancellationToken token)
    {
        var cache = _cache.GetOrAdd(server.ToString(), _ => new CacheEntry());
        if (cache.MapExpiresAt > DateTime.UtcNow && cache.Map is not null)
        {
            return cache.Map;
        }

        var world = await GetPreferenceAsync(server, "GameWorld", token) ?? "Unknown";
        var saveName = await GetPreferenceAsync(server, "GameName", token) ?? world;
        var worldSizeText = await GetPreferenceAsync(server, "WorldGenSize", token);
        var worldSize = int.TryParse(worldSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var parsedWorldSize) ? Math.Max(1024, parsedWorldSize) : 6144;
        var displayName = string.Equals(world, "RWG", StringComparison.OrdinalIgnoreCase) ? saveName : world;

        cache.Map = new
        {
            provider = "d7d",
            name = displayName,
            alias = displayName,
            saveName,
            mapSize = new { x = worldSize, y = 255, z = worldSize },
            tileSize = 128,
            maxZoom = 4
        };
        cache.MapExpiresAt = DateTime.UtcNow + MapLifetime;
        return cache.Map;
    }

    public async Task<object> GetPlayersAsync(Server server, CancellationToken token)
    {
        var cache = _cache.GetOrAdd(server.ToString(), _ => new CacheEntry());
        if (cache.PlayerExpiresAt > DateTime.UtcNow && cache.Players is not null)
        {
            return cache.Players;
        }

        var response = await server.ExecuteCommandAsync("listplayers", token);
        cache.Players = SevenDaysToDiePlayerParser.Parse(string.Join(Environment.NewLine, response))
            .Select(player => new
            {
                name = player.CurrentAlias.Name,
                guid = player.EntityId,
                location = new { x = player.Position.X, y = player.Position.Y, z = player.Position.Z },
                viewAngles = new { x = player.Rotation.X, y = player.Rotation.Y, z = player.Rotation.Z },
                radianAngles = new
                {
                    x = player.Rotation.X * Math.PI / 180,
                    y = player.Rotation.Y * Math.PI / 180,
                    z = player.Rotation.Z * Math.PI / 180
                },
                team = "survivors",
                kills = player.ZombieKills,
                deaths = player.PlayerDeaths,
                score = player.Score,
                playTime = 0,
                weapon = "none",
                health = player.Health,
                isAlive = player.Health > 0,
                id = FormattableString.Invariant(
                    $"{player.EntityId}:{player.Position.X:F1}:{player.Position.Z:F1}:{player.Rotation.Y:F1}")
            }).ToArray();
        cache.PlayerExpiresAt = DateTime.UtcNow + PlayerLifetime;
        return cache.Players;
    }

    public async Task<byte[]> GetTileAsync(Server server, int zoom, int tileX, int tileY,
        CancellationToken token)
    {
        var configuration = server.ServerConfig.ExternalWeb ??
                            throw new InvalidOperationException("ExternalWeb is not configured for this server");
        if (!IsSupportedUrl(configuration.Url))
        {
            throw new InvalidOperationException("ExternalWeb.Url must be an absolute HTTP or HTTPS URL");
        }

        var tileUrl = new Uri(EnsureTrailingSlash(configuration.Url), $"map/{zoom}/{tileX}/{tileY}.png");
        using var request = new HttpRequestMessage(HttpMethod.Get, tileUrl);
        if (!string.IsNullOrWhiteSpace(configuration.TokenName) ||
            !string.IsNullOrWhiteSpace(configuration.TokenFile))
        {
            if (string.IsNullOrWhiteSpace(configuration.TokenName) || configuration.TokenName.Length > 256 ||
                configuration.TokenName.Contains('\r') || configuration.TokenName.Contains('\n') ||
                string.IsNullOrWhiteSpace(configuration.TokenFile))
            {
                throw new InvalidOperationException("ExternalWeb token configuration is incomplete or invalid");
            }

            var secretInfo = new FileInfo(configuration.TokenFile);
            if (!secretInfo.Exists || secretInfo.Length is <= 0 or > MaxSecretBytes)
            {
                throw new InvalidOperationException("ExternalWeb token file is missing or invalid");
            }

            var secret = (await File.ReadAllTextAsync(configuration.TokenFile, token)).Trim();
            if (string.IsNullOrEmpty(secret) || secret.Contains('\r') || secret.Contains('\n'))
            {
                throw new InvalidOperationException("ExternalWeb token file is empty or invalid");
            }

            request.Headers.Add("X-SDTD-API-TOKENNAME", configuration.TokenName);
            request.Headers.Add("X-SDTD-API-SECRET", secret);
        }

        using var response = await httpClientFactory.CreateClient("LiveRadar7DTD").SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "image/png",
                StringComparison.OrdinalIgnoreCase) || response.Content.Headers.ContentLength is > MaxTileBytes)
        {
            throw new InvalidDataException("External web endpoint did not return a valid PNG tile");
        }

        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > MaxTileBytes)
            {
                throw new InvalidDataException("Live Radar tile exceeded the size limit");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }

        return output.ToArray();
    }

    private static async Task<string> GetPreferenceAsync(Server server, string name, CancellationToken token)
    {
        var response = await server.ExecuteCommandAsync($"getgamepref {name}", token);
        const string separator = "=";
        var line = response.FirstOrDefault(item => item.Contains($"GamePref.{name}", StringComparison.OrdinalIgnoreCase));
        var separatorIndex = line?.IndexOf(separator, StringComparison.Ordinal) ?? -1;
        return separatorIndex >= 0 ? line![(separatorIndex + separator.Length)..].Trim() : null;
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");

    private static bool IsSupportedUrl(Uri uri) => uri is { IsAbsoluteUri: true } &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private sealed class CacheEntry
    {
        public DateTime MapExpiresAt { get; set; }
        public object Map { get; set; }
        public DateTime PlayerExpiresAt { get; set; }
        public object Players { get; set; }
    }
}
