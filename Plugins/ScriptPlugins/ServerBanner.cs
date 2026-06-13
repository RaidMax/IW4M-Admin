#:package RaidMax.IW4MAdmin.SharedLibraryCore@2026.1.6.1

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

/// <summary>
/// Server Banner plugin - exposes embeddable HTML banners (and a webfront preview page) for each
/// monitored game server, with geo-location flag lookup. Ported from the legacy ServerBanner.js
/// Jint script.
/// </summary>
public class ServerBannerPlugin : IPluginV2
{
    public string Name => "Server Banner";
    public string Author => "RaidMax";
    public string Version => "1.2";

    private const string BannerKey = "Banner";
    private const string BannerPreviewKey = "Webfront::Nav::Main::BannerPreview";
    private const string LocationPending = "SO";
    private const string Font = "Noto Sans Mono";

    private const string ColorLeft = "color: #f5f5f5; text-shadow: -1px 1px 8px #000000cc;";
    private const string ColorRight = "color: #222222; text-shadow: -1px 1px 8px #ecececcc;";

    // per-game overrides of the default left/right text colors (null = keep default)
    private static readonly Dictionary<string, (string? Left, string? Right)> ColorOverrides = new()
    {
        ["t6"] = (null, ColorLeft),
        ["iw3"] = (ColorRight, null),
        ["iw5"] = (ColorRight, null),
        ["iw6"] = (null, ColorLeft),
        ["t4"] = (ColorRight, ColorLeft),
        ["t5"] = (null, ColorLeft),
        ["t7"] = (null, ColorLeft),
        ["shg1"] = (null, ColorLeft),
        ["h1"] = (null, ColorLeft),
        ["csgo"] = (null, ColorLeft),
        ["h2m"] = (null, ColorLeft),
        ["iw7"] = (null, ColorLeft)
    };

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    // listenAddress -> country code (or LocationPending while a lookup is in flight)
    private readonly ConcurrentDictionary<string, string> _serverLocationCache = new();
    // lowercase game code -> servers of that game, ordered by player count desc
    private readonly object _orderLock = new();
    private readonly Dictionary<string, List<Server>> _serverOrderCache = new();

    private readonly ILogger<ServerBannerPlugin> _logger;
    private readonly IManager _manager;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly ApplicationConfiguration _appConfig;

    public ServerBannerPlugin(
        ILogger<ServerBannerPlugin> logger,
        IManager manager,
        IInteractionRegistration interactionRegistration)
    {
        _logger = logger;
        _manager = manager;
        _interactionRegistration = interactionRegistration;
        _appConfig = manager.GetApplicationSettings().Configuration();

        IGameServerEventSubscriptions.MonitoringStarted += OnMonitoringStarted;
        IGameServerEventSubscriptions.ServerRemoved += OnServerRemoved;

        RegisterInteractions();

        _logger.LogInformation("{Name} {Version} by {Author} loaded", Name, Version, Author);
    }

    private async Task OnMonitoringStarted(MonitorStartEvent startEvent, CancellationToken token)
    {
        if (startEvent.Server is Server server)
        {
            await EnsureServerCachedAsync(server, token);
        }
    }

    private Task OnServerRemoved(ServerRemoveEvent removedEvent, CancellationToken token)
    {
        if (removedEvent.Server is not Server server)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("[ServerBanner] cleaning up cache for removed server {ServerId}", server.Id);

        var gameCode = GameCode(server);
        lock (_orderLock)
        {
            if (_serverOrderCache.TryGetValue(gameCode, out var list))
            {
                list.RemoveAll(s => s.Id == server.Id);
                if (list.Count == 0)
                {
                    _serverOrderCache.Remove(gameCode);
                }
            }
        }

        var addressStillInUse = _manager.Servers.Any(s => s.Id != server.Id && s.ListenAddress == server.ListenAddress);
        if (!addressStillInUse)
        {
            _serverLocationCache.TryRemove(server.ListenAddress, out _);
        }

        return Task.CompletedTask;
    }

    private async Task EnsureServerCachedAsync(Server server, CancellationToken token)
    {
        var gameCode = GameCode(server);

        var needLookup = false;
        if (!_serverLocationCache.ContainsKey(server.ListenAddress))
        {
            _serverLocationCache[server.ListenAddress] = LocationPending;
            needLookup = true;
        }

        lock (_orderLock)
        {
            if (!_serverOrderCache.TryGetValue(gameCode, out var list))
            {
                list = new List<Server>();
                _serverOrderCache[gameCode] = list;
            }

            if (list.All(s => s.Id != server.Id))
            {
                list.Add(server);
            }

            list.Sort((a, b) => b.ClientNum - a.ClientNum);
        }

        if (!needLookup)
        {
            return;
        }

        var lookupIp = server.ResolvedIpEndPoint.Address.IsInternal()
            ? _manager.ExternalIPAddress
            : server.ResolvedIpEndPoint.Address.ToString();

        _logger.LogInformation("Looking up server location for IP {IP}", lookupIp);

        try
        {
            var result = (await HttpClient.GetStringAsync($"https://ipinfo.io/{lookupIp}/country", token)).Trim();

            // a valid response is a plain country code (e.g. "US"); an error is returned as JSON
            if (!string.IsNullOrWhiteSpace(result) && !IsJson(result))
            {
                _serverLocationCache[server.ListenAddress] = result;
            }
            else
            {
                _logger.LogWarning("Could not determine server location from IP");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine server location from IP");
        }
    }

    private void RegisterInteractions()
    {
        _interactionRegistration.UnregisterInteraction(BannerKey);
        _interactionRegistration.UnregisterInteraction(BannerPreviewKey);

        // raw HTML banner for a single server, rendered in an iframe
        _interactionRegistration.RegisterInteraction(BannerKey, (clientId, game, token) =>
        {
            var interaction = new InteractionData
            {
                InteractionId = "banner",
                MinimumPermission = EFClient.Permission.User,
                InteractionType = InteractionType.RawContent,
                Source = Name
            };

            interaction.Action = async (sourceId, targetId, g, meta, ct) =>
            {
                if (meta is null || !meta.TryGetValue("serverId", out var serverId))
                {
                    return string.Empty;
                }

                var isSmall = meta.TryGetValue("size", out var size) && size == "small";
                var server = _manager.Servers.FirstOrDefault(s => s.Id == serverId);
                if (server is null)
                {
                    return string.Empty;
                }

                if (!_serverLocationCache.ContainsKey(server.ListenAddress) || !OrderCacheContainsGame(server))
                {
                    await EnsureServerCachedAsync(server, ct);
                }

                return BuildBannerHtml(server, isSmall);
            };

            return Task.FromResult<IInteractionData>(interaction);
        });

        // webfront nav page previewing every server's banner with copy-able embed code
        _interactionRegistration.RegisterInteraction(BannerPreviewKey, (clientId, game, token) =>
        {
            var interaction = new InteractionData
            {
                InteractionId = BannerPreviewKey,
                MinimumPermission = EFClient.Permission.User,
                InteractionType = InteractionType.TemplateContent,
                Source = Name,
                Name = "Banners",
                Description = "Banners",
                DisplayMeta = "ph-image"
            };

            interaction.Action = async (sourceId, targetId, g, meta, ct) =>
            {
                bool empty;
                lock (_orderLock)
                {
                    empty = _serverOrderCache.Count == 0;
                }

                if (empty)
                {
                    foreach (var server in _manager.Servers)
                    {
                        await EnsureServerCachedAsync(server, ct);
                    }
                }

                List<Server> snapshot;
                lock (_orderLock)
                {
                    snapshot = _serverOrderCache.Values.SelectMany(list => list).ToList();
                }

                var response = new StringBuilder("<div class=\"grid grid-cols-1 gap-6\"><script></script>");
                foreach (var server in snapshot)
                {
                    response.Append(BuildPreviewCard(server));
                }

                response.Append("</div>");
                return response.ToString();
            };

            return Task.FromResult<IInteractionData>(interaction);
        });
    }

    private bool OrderCacheContainsGame(Server server)
    {
        lock (_orderLock)
        {
            return _serverOrderCache.ContainsKey(GameCode(server));
        }
    }

    private static string GameCode(Server server) => server.GameCode.ToString().ToLowerInvariant();

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string BuildBannerHtml(Server server, bool isSmall)
    {
        var gameCode = GameCode(server);

        var colorLeft = ColorLeft;
        var colorRight = ColorRight;
        if (ColorOverrides.TryGetValue(gameCode, out var ov))
        {
            colorLeft = ov.Left ?? colorLeft;
            colorRight = ov.Right ?? colorRight;
        }

        var displayIp = server.ResolvedIpEndPoint.Address.IsInternal()
            ? _manager.ExternalIPAddress
            : server.ResolvedIpEndPoint.Address.ToString();

        var location = _serverLocationCache.GetValueOrDefault(server.ListenAddress);
        var locationLower = location?.ToLowerInvariant() ?? string.Empty;
        var serverName = server.ServerName.StripColors();
        var mapAlias = server.Map?.Alias ?? string.Empty;
        var players = server.Throttled ? "-" : server.ClientNum.ToString();

        var status = server.Throttled
            ? (isSmall ? "<div class=\"status small offline-x\"></div>" : "<div class=\"status-offline subtitle\">OFFLINE</div>")
            : (isSmall ? "<div class=\"status small online-checkmark\"></div>" : "<div class=\"status-online subtitle\">ONLINE</div>");

        var head = $$"""
            <head>
                <link rel="stylesheet" href="https://fonts.googleapis.com/css?family={{Font}}">
                    <style>
                        * {
                            padding: 0;
                            margin: 0;
                        }
                        .server-container {
                            font-family: '{{Font}}';
                            background: url('images/banners/{{gameCode}}.jpg') no-repeat;
                            align-items: center;
                        }
                        .server-container.large {
                            padding-left: 1rem;
                            padding-right: 1rem;
                            height: 120px;
                            display: flex;
                            background-position: center center;
                        }
                        .server-container.small {
                            padding: 0.5rem;
                            background-position: left center;
                        }
                        .game-icon {
                            background: url('images/icons/{{gameCode}}.jpg') no-repeat;
                            background-size: contain;
                        }
                        .game-icon.large {
                            width: 64px;
                            height: 64px;
                            border-radius: 10px;
                        }
                        .game-icon.small {
                            width: 20px;
                            height: 20px;
                            border-radius: 5px;
                        }
                        .first-line.small, .second-line.small {
                            display: flex;
                            flex-direction: row;
                        }
                        .first-line.small .header {
                            font-size: 10pt;
                            font-weight: bold;
                            margin-left: 0.5rem;
                            align-self: center;
                        }
                        .second-line.small {
                            align-self: center;
                        }
                        .game-info.small {
                            margin-left: 0.5rem;
                            font-size: 9pt;
                        }
                        .game-info.large {
                            padding: 0 0.75em;
                        }
                        .game-info .header {
                            font-weight: bold;
                        }
                        img.location-image {
                            width: 20px;
                            align-self: center;
                        }
                        .game-info.large .subtitle {
                            font-size: 0.9rem;
                        }
                        .text-weight-lighter {
                            font-weight: lighter
                        }
                        .status-online {
                            color: green;
                        }
                        .status-offline {
                            color: red;
                        }
                        .players-flag-section {
                            flex: 1;
                            display:flex;
                            flex-direction: row;
                            align-items: center;
                        }
                        .players-flag-section img {
                            margin: 0 0.5rem;
                            height: 0.75rem;
                        }
                        .status.small:after {
                            position: absolute;
                            width: 20px;
                            height: 15px;
                            text-align: center;
                            border-radius: 2px;
                            font-size: 8pt;
                            margin-top: 0.1rem;
                            margin-left: -0.5rem;
                        }
                        .online-checkmark:after {
                            content: '\2714';
                            color: white;
                            background: rgba(0, 128, 0, 0.5);
                        }
                        .offline-x:after {
                            content: '\2715';
                            color: white;
                            background: rgba(128, 0, 0, 0.5);
                        }
                        h3, .server-container.large div {
                            line-height: 1.5rem;
                        }
                        h2 {
                            line-height: 2rem;
                        }
                    </style>
                    <title>{{displayIp}}:{{server.ListenPort}}</title>
            </head>
            """;

        if (isSmall)
        {
            return $$"""
                <html lang="en">
                    {{head}}
                    <body>
                        <div class="server-container small" id="server">
                            <div class="first-line small">
                                <div class="game-icon small"></div>
                                <div class="header" id="serverName" style="{{colorLeft}}">{{serverName}}</div>
                            </div>
                            <div class="third-line game-info small">
                                {{status}}
                                <div style="{{colorLeft}}; margin-left: 20px;">{{displayIp}}:{{server.ListenPort}}</div>
                            </div>
                            <div class="second-line small">
                                <img src="https://flagcdn.com/w40/{{locationLower}}.png"
                                     alt="{{location}}" class="location-image">
                                <div class="game-info small" style="{{colorLeft}}">
                                    <span>{{players}}/{{server.MaxClients}}</span>
                                    &bullet;
                                    <span>{{mapAlias}}</span>
                                    &bullet;
                                    <span>{{server.GametypeName}}</span>
                                </div>
                            </div>
                        </div>
                    </body>
                </html>
                """;
        }

        return $$"""
            <html lang="en">
                {{head}}
                <body>
                    <div class="server-container large" id="server">
                            <div class="game-icon large"
                                style="background: url('images/icons/{{gameCode}}.jpg');">
                            </div>
                            <div style="flex: 1; {{colorLeft}}" class="game-info large">
                                <div class="header" id="serverName">{{serverName}}</div>
                                <div class="text-weight-lighter subtitle">{{displayIp}}:{{server.ListenPort}}</div>
                                <div class="players-flag-section">
                                    <div class="subtitle">{{players}}/{{server.MaxClients}} Players</div>
                                    <img src="https://flagcdn.com/h20/{{locationLower}}.png"
                                         alt="{{location}}"/>
                                </div>
                            </div>
                            <div style="{{colorRight}}; text-align: right;" class="game-info">
                                <div class="header">{{mapAlias}}</div>
                                <div class="text-weight-lighter subtitle">{{server.GametypeName}}</div>
                                {{status}}
                            </div>
                    </div>
                </body>
            </html>
            """;
    }

    private string BuildPreviewCard(Server server)
    {
        var serverName = server.ServerName.StripColors();
        var gameCode = server.GameCode.ToString();
        var id = server.Id;
        var webfrontUrl = _appConfig.WebfrontUrl;

        return $$"""
            <div class="bg-surface rounded-lg border border-line shadow-sm p-5">
                <div class="text-base mb-4 flex items-center justify-between">
                    <span class="font-medium text-foreground">{{serverName}}</span>
                    <span class="px-2 py-1 rounded bg-surface-alt text-sm font-mono text-muted border border-line">{{gameCode}}</span>
                </div>
                <div class="overflow-hidden mb-4">
                    <iframe src="/Interaction/Render/Banner?serverId={{id}}" width="750"
                            height="120" style="border-width: 0; overflow: hidden;"
                            class="rounded-lg"
                            title="{{id}}"></iframe>
                </div>
                <button type="button" class="px-4 py-2 rounded-lg bg-primary text-white hover:bg-action-primary-hover transition-colors text-sm font-medium mb-4" onclick="document.getElementById('showCode{{id}}').classList.toggle('hidden')">Show Embed</button>
                <div class="hidden p-4 mb-4 bg-surface-alt rounded-lg border border-line font-mono text-xs text-muted overflow-x-auto" id="showCode{{id}}">
                    &lt;iframe
                    <br/>&nbsp;src="{{webfrontUrl}}/Interaction/Render/Banner?serverId={{id}}"
                    <br/>&nbsp;width="750" height="120" style="border-width: 0; overflow: hidden;"&gt;<br/>
                    &lt;/iframe&gt;</div>
                <div class="rounded-lg overflow-hidden mb-4 mt-4">
                    <iframe src="/Interaction/Render/Banner?serverId={{id}}&size=small" width="400"
                            height="70" style="border-width: 0; overflow: hidden;"
                            class="rounded-lg"
                            title="{{id}}"></iframe>
                </div>
                <button type="button" class="px-4 py-2 rounded-lg bg-primary text-white hover:bg-action-primary-hover transition-colors text-sm font-medium mb-4" onclick="document.getElementById('showCode{{id}}Small').classList.toggle('hidden')">Show Embed</button>
                <div class="hidden p-4 bg-surface-alt rounded-lg border border-line font-mono text-xs text-muted overflow-x-auto" id="showCode{{id}}Small">
                    &lt;iframe
                    <br/>&nbsp;src="{{webfrontUrl}}/Interaction/Render/Banner?serverId={{id}}&size=small"
                    <br/>&nbsp;width="400" height="70" style="border-width: 0; overflow: hidden;"&gt;<br/>
                    &lt;/iframe&gt;</div>
            </div>
            """;
    }

    public void Dispose()
    {
        IGameServerEventSubscriptions.MonitoringStarted -= OnMonitoringStarted;
        IGameServerEventSubscriptions.ServerRemoved -= OnServerRemoved;
        _interactionRegistration.UnregisterInteraction(BannerKey);
        _interactionRegistration.UnregisterInteraction(BannerPreviewKey);
        _logger.LogInformation("{Name} unloaded", Name);
    }
}
