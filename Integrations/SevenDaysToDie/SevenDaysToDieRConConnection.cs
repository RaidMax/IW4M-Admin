using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;

namespace Integrations.SevenDaysToDie;

public sealed partial class SevenDaysToDieRConConnection : IRConConnection
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseQuietPeriod = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MetadataLifetime = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions RadarJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPEndPoint _endpoint;
    private readonly string _password;
    private readonly ILogger<SevenDaysToDieRConConnection> _logger;
    private readonly SemaphoreSlim _queryLock = new(1, 1);
    private readonly Dictionary<int, SevenDaysToDiePlayer> _playersBySlot = new();

    private TcpClient _client;
    private NetworkStream _stream;
    private DateTime _metadataExpiresAt;
    private string _hostname = "7 Days to Die Server";
    private string _map = "Unknown";
    private int _maxPlayers = 8;
    private int _worldSize = 6144;
    private string _world = "Unknown";
    private string _saveName = "Unknown";
    private string _version = "7DTD";
    private bool _disposed;

    public SevenDaysToDieRConConnection(IPEndPoint endpoint, string password,
        ILogger<SevenDaysToDieRConConnection> logger)
    {
        _endpoint = endpoint;
        _password = password ?? string.Empty;
        _logger = logger;
    }

    public TimeSpan? LastRtt { get; private set; }

    public async Task<string[]> SendQueryAsync(StaticHelpers.QueryType type, string parameters = "",
        CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _queryLock.WaitAsync(token);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await SendQueryCoreAsync(type, parameters ?? string.Empty, token);
            stopwatch.Stop();
            LastRtt = stopwatch.Elapsed;
            return result;
        }
        catch (ServerException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Disconnect();
            throw new NetworkException($"Timed out communicating with 7 Days to Die server {_endpoint}");
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            Disconnect();
            _logger.LogError(exception, "Could not communicate with 7 Days to Die server {Endpoint}", _endpoint);
            throw new NetworkException($"Unable to communicate with 7 Days to Die server {_endpoint}");
        }
        finally
        {
            _queryLock.Release();
        }
    }

    public void SetConfiguration(IRConParser config)
    {
    }

    private async Task<string> BuildLiveRadarMapJsonAsync(CancellationToken token)
    {
        await RefreshMetadataAsync(token);
        return JsonSerializer.Serialize(new
        {
            provider = "d7d",
            name = _world,
            alias = _world,
            saveName = _saveName,
            mapSize = new { x = _worldSize, y = 255, z = _worldSize },
            tileSize = 128,
            maxZoom = 4
        }, RadarJsonOptions);
    }

    private async Task<string> BuildLiveRadarDataJsonAsync(CancellationToken token)
    {
        var players = await RefreshPlayersAsync(token);
        var payload = players.Select(player => new
        {
            name = player.Name,
            guid = player.EntityId,
            location = new { x = player.PositionX, y = player.PositionY, z = player.PositionZ },
            viewAngles = new { x = player.RotationX, y = player.RotationY, z = player.RotationZ },
            radianAngles = new
            {
                x = player.RotationX * Math.PI / 180,
                y = player.RotationY * Math.PI / 180,
                z = player.RotationZ * Math.PI / 180
            },
            team = "survivors",
            kills = player.ZombieKills,
            deaths = player.Deaths,
            score = player.Level,
            playTime = 0,
            weapon = "none",
            health = player.Health,
            isAlive = player.Health > 0,
            id = FormattableString.Invariant(
                $"{player.EntityId}:{player.PositionX:F1}:{player.PositionZ:F1}:{player.RotationY:F1}")
        });

        return JsonSerializer.Serialize(payload, RadarJsonOptions);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disconnect();
        _queryLock.Dispose();
    }

    private async Task<string[]> SendQueryCoreAsync(StaticHelpers.QueryType type, string parameters,
        CancellationToken token)
    {
        return type switch
        {
            StaticHelpers.QueryType.COMMAND_STATUS => await BuildStatusAsync(token),
            StaticHelpers.QueryType.GET_DVAR => [BuildDvarResponse(parameters, await GetDvarValueAsync(parameters, token))],
            StaticHelpers.QueryType.GET_INFO => await BuildStatusAsync(token),
            StaticHelpers.QueryType.SET_DVAR => ["Unsupported command"],
            _ => SplitResponse(await ExecuteIw4MAdminCommandAsync(parameters, token))
        };
    }

    private async Task<string[]> BuildStatusAsync(CancellationToken token)
    {
        await RefreshMetadataAsync(token);
        var players = await RefreshPlayersAsync(token);

        var response = new List<string>
        {
            $"hostname: {_hostname}",
            $"map: {_map}",
            "gametype: Survival",
            $"players: {players.Count}/{_maxPlayers}",
            "slot score kills deaths ping networkid name address"
        };

        response.AddRange(players.Select(player =>
            $"{player.Slot} {player.Level} {player.ZombieKills} {player.Deaths} {player.Ping} {player.NetworkId} \"{SanitizeName(player.Name)}\" {player.Address}:0"));
        return response.ToArray();
    }

    private async Task<string> GetDvarValueAsync(string name, CancellationToken token)
    {
        name = name.Trim();
        if (name is "sv_hostname" or "hostname" or "mapname" or "sv_maxclients" or "version")
        {
            await RefreshMetadataAsync(token);
        }

        return name.ToLowerInvariant() switch
        {
            "version" => _version,
            "sv_running" => "1",
            "sv_hostname" or "hostname" => _hostname,
            "mapname" => _map,
            "sv_maxclients" => _maxPlayers.ToString(CultureInfo.InvariantCulture),
            "g_gametype" or "gametype" => "Survival",
            "net_ip" => "0.0.0.0",
            "g_logsync" => "2",
            "sv_privateclients" => "0",
            "fs_basepath" or "fs_basegame" or "fs_homepath" or "fs_game" or "g_log" or "g_password" => "",
            _ => "Unknown command"
        };
    }

    private async Task RefreshMetadataAsync(CancellationToken token)
    {
        if (_metadataExpiresAt > DateTime.UtcNow)
        {
            return;
        }

        _hostname = ParsePreference(await ExecuteTelnetCommandAsync("getgamepref ServerName", token),
            "ServerName") ?? _hostname;
        var world = ParsePreference(await ExecuteTelnetCommandAsync("getgamepref GameWorld", token), "GameWorld");
        var saveName = ParsePreference(await ExecuteTelnetCommandAsync("getgamepref GameName", token), "GameName");
        _world = world ?? _world;
        _saveName = saveName ?? _saveName;
        _map = string.Equals(world, "RWG", StringComparison.OrdinalIgnoreCase)
            ? saveName ?? world ?? _map
            : world ?? saveName ?? _map;

        var maxPlayers = ParsePreference(
            await ExecuteTelnetCommandAsync("getgamepref ServerMaxPlayerCount", token), "ServerMaxPlayerCount");
        if (int.TryParse(maxPlayers, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxPlayers))
        {
            _maxPlayers = parsedMaxPlayers;
        }

        var worldSize = ParsePreference(await ExecuteTelnetCommandAsync("getgamepref WorldGenSize", token),
            "WorldGenSize");
        if (int.TryParse(worldSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWorldSize))
        {
            _worldSize = Math.Max(1024, parsedWorldSize);
        }

        _metadataExpiresAt = DateTime.UtcNow + MetadataLifetime;
    }

    private async Task<IReadOnlyList<SevenDaysToDiePlayer>> RefreshPlayersAsync(CancellationToken token)
    {
        var players = SevenDaysToDiePlayerParser.Parse(await ExecuteTelnetCommandAsync("listplayers", token));
        _playersBySlot.Clear();
        foreach (var player in players)
        {
            _playersBySlot[player.Slot] = player;
        }

        return players;
    }

    private async Task<string> ExecuteIw4MAdminCommandAsync(string command, CancellationToken token)
    {
        command = CleanConsoleText(command);
        var commandMatch = CommandRegex().Match(command);
        if (!commandMatch.Success)
        {
            return string.Empty;
        }

        var verb = commandMatch.Groups["verb"].Value.ToLowerInvariant();
        var arguments = commandMatch.Groups["arguments"].Value.Trim();

        if (verb == "livemap")
        {
            return await BuildLiveRadarMapJsonAsync(token);
        }

        if (verb == "liveradar")
        {
            return await BuildLiveRadarDataJsonAsync(token);
        }

        if (verb == "say")
        {
            var message = CleanConsoleText(Unquote(arguments));
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogDebug("Ignoring empty 7 Days to Die broadcast");
                return string.Empty;
            }

            return await ExecuteTelnetCommandAsync($"say {Quote(message)}", token);
        }

        if (verb is "tell" or "kick" or "clientkick")
        {
            var playerCommand = PlayerCommandRegex().Match(arguments);
            if (!playerCommand.Success || !int.TryParse(playerCommand.Groups["slot"].Value, out var slot))
            {
                return "Player not found";
            }

            if (!_playersBySlot.TryGetValue(slot, out var player))
            {
                await BuildStatusAsync(token);
                _playersBySlot.TryGetValue(slot, out player);
            }

            if (player is null)
            {
                return "Player not found";
            }

            var message = CleanConsoleText(Unquote(playerCommand.Groups["message"].Value.Trim()));
            if (verb == "tell" && string.IsNullOrWhiteSpace(message))
            {
                _logger.LogDebug("Ignoring empty 7 Days to Die private message for slot {Slot}", slot);
                return string.Empty;
            }

            return verb == "tell"
                ? await ExecuteTelnetCommandAsync($"sayplayer {player.EntityId} {Quote(message)}", token)
                : await ExecuteTelnetCommandAsync(
                    $"kick {Quote(player.Name)} {Quote(string.IsNullOrWhiteSpace(message) ? "Kicked by administrator" : message)}",
                    token);
        }

        return await ExecuteTelnetCommandAsync(command, token);
    }

    private async Task<string> ExecuteTelnetCommandAsync(string command, CancellationToken token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await EnsureConnectedAsync(token);
                var bytes = Encoding.UTF8.GetBytes(command + "\n");
                await _stream.WriteAsync(bytes, token);
                await _stream.FlushAsync(token);
                return await ReadResponseAsync(token);
            }
            catch (Exception exception) when (attempt == 0 && exception is IOException or SocketException)
            {
                Disconnect();
            }
        }

        throw new NetworkException($"Unable to communicate with 7 Days to Die server {_endpoint}");
    }

    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        if (_client?.Connected == true && _stream is not null)
        {
            return;
        }

        Disconnect();
        _client = new TcpClient(_endpoint.AddressFamily);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(ConnectionTimeout);
        await _client.ConnectAsync(_endpoint.Address, _endpoint.Port, timeout.Token);
        _stream = _client.GetStream();

        var banner = await ReadResponseAsync(timeout.Token);
        if (banner.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            var passwordBytes = Encoding.UTF8.GetBytes(_password + "\n");
            await _stream.WriteAsync(passwordBytes, timeout.Token);
            await _stream.FlushAsync(timeout.Token);
            var loginResponse = await ReadResponseAsync(timeout.Token);
            if (loginResponse.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                loginResponse.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                Disconnect();
                throw new ServerException($"Could not authenticate to 7 Days to Die server {_endpoint}");
            }
        }
    }

    private async Task<string> ReadResponseAsync(CancellationToken token)
    {
        var response = new MemoryStream();
        var buffer = new byte[16 * 1024];
        var receivedData = false;
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < ResponseTimeout)
        {
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            readTimeout.CancelAfter(receivedData ? ResponseQuietPeriod : ResponseTimeout);

            try
            {
                var count = await _stream.ReadAsync(buffer, readTimeout.Token);
                if (count == 0)
                {
                    throw new IOException("7 Days to Die Telnet connection closed");
                }

                await response.WriteAsync(buffer.AsMemory(0, count), token);
                receivedData = true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                break;
            }
        }

        return StripTelnetNegotiation(response.ToArray());
    }

    private static string StripTelnetNegotiation(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] != 255 || index + 1 >= data.Length)
            {
                output.WriteByte(data[index]);
                continue;
            }

            var command = data[++index];
            if (command is 251 or 252 or 253 or 254 && index + 1 < data.Length)
            {
                index++;
            }
            else if (command == 250)
            {
                while (index + 1 < data.Length && !(data[index] == 255 && data[index + 1] == 240))
                {
                    index++;
                }

                index++;
            }
            else if (command == 255)
            {
                output.WriteByte(255);
            }
        }

        return Encoding.UTF8.GetString(output.ToArray()).Replace("\0", string.Empty).Trim();
    }

    private void Disconnect()
    {
        try
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
        catch
        {
            // Ignore disposal errors while replacing a failed connection.
        }
        finally
        {
            _stream = null;
            _client = null;
        }
    }

    private static string BuildDvarResponse(string name, string value) =>
        value == "Unknown command"
            ? value
            : $"\"{name.Trim()}\" is: \"{value}\" default: \"{value}\"";

    private static string ParsePreference(string response, string preference)
    {
        var match = Regex.Match(response, $@"GamePref\.{Regex.Escape(preference)}\s*=\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim().Trim('\r') : null;
    }

    private static string[] SplitResponse(string response) => response
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CleanConsoleText(string value) => ColorRegex().Replace(value ?? string.Empty, string.Empty)
        .Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string SanitizeName(string value) => CleanConsoleText(value).Replace('"', '\'');

    private static string Quote(string value) => $"\"{CleanConsoleText(value).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
        ? value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\")
        : value;

    [GeneratedRegex(@"^(?<verb>\S+)(?:\s+(?<arguments>.*))?$")]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"^(?<slot>\d+)(?:\s+(?<message>.*))?$")]
    private static partial Regex PlayerCommandRegex();

    [GeneratedRegex(@"\^\d|\(Color::[A-Za-z]+\)")]
    private static partial Regex ColorRegex();
}
