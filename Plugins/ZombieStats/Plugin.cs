using Data.Abstractions;
using Data.Models;
using Data.Models.Client.Stats;
using Humanizer;
using IW4MAdmin.Plugins.ZombieStats.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Game.GameScript;
using SharedLibraryCore.Events.Game.GameScript.Zombie;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Plugins.ZombieStats;

public class Plugin : IPluginV2
{
    private readonly ILogger<Plugin> _logger;
    private readonly ZombieEventParser _zombieEventParser;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IZombieStatsEnhancer? _enhancer;
    private readonly HashSet<long> _knownZombieServerIds = [];

    // Servers currently being processed via the session cache despite the live
    // IsZombieServer() gate returning false (a transient non-zombie gametype from
    // the RCon status poll). Used to warn once per fallback episode and to detect
    // recovery. See ShouldProcessZombieEvent / round-freeze incident 2026-06-20.
    private readonly HashSet<long> _gametypeFallbackActive = [];

    // Guards both zombie-server sets above; the event handlers run concurrently
    // (CoreEventHandler dispatches up to MaxCurrentEvents in parallel).
    private readonly Lock _zombieServerStateLock = new();

    public string Name { get; } = nameof(Plugin).Titleize();
    public string Author => "RaidMax";
    public string Version => Utilities.GetVersionAsString();

    public Plugin(ILogger<Plugin> logger, ZombieEventParser zombieEventParser,
        IDatabaseContextFactory contextFactory, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _zombieEventParser = zombieEventParser;
        _contextFactory = contextFactory;
        _enhancer = serviceProvider.GetService(typeof(IZombieStatsEnhancer)) as IZombieStatsEnhancer;

        IManagementEventSubscriptions.Load += OnLoad;
        IManagementEventSubscriptions.Unload += OnUnload;
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientAuthorized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientDisposed;
        // Periodic per-server client-data poll — used as the reconciliation heartbeat so a
        // player the edge-triggered ClientStateAuthorized path missed still gets tracked
        // (and appears in the live modal) within a poll cycle. See ReconcileConnectedClients.
        IGameServerEventSubscriptions.ClientDataUpdated += OnClientDataUpdated;
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEvent;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IGameEventSubscriptions.MatchStarted += OnMatchStarted;
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ZombieEventParser>();
    }

    /// <summary>
    /// Decides whether zombie processing should run for <paramref name="server"/>,
    /// tolerating a transient non-zombie gametype reported by the RCon status poll.
    /// </summary>
    /// <remarks>
    /// The live <see cref="Utilities.IsZombieServer(IGameServer)"/> check reads
    /// <c>server.Gametype</c>, refreshed from the status poll. A single poll returning a
    /// non-zombie (but non-empty) gametype would otherwise silently sever ALL zombie
    /// processing for the rest of the match — the round-progression freeze observed
    /// 2026-06-20, where a T5 Moon match stopped advancing at round 10 while the game ran
    /// to 25 and nothing was logged. Once a server has been confirmed zombie this session
    /// (the cache is never cleared) keep processing its events even if a later poll
    /// disagrees, and warn once with the offending gametype so the cause is visible.
    /// </remarks>
    private bool ShouldProcessZombieEvent(IGameServer? server)
    {
        if (server is null)
        {
            return false;
        }

        var serverId = server.LegacyDatabaseId;

        if (server.IsZombieServer())
        {
            bool recovered;
            lock (_zombieServerStateLock)
            {
                _knownZombieServerIds.Add(serverId);
                recovered = _gametypeFallbackActive.Remove(serverId);
            }

            if (recovered)
            {
                _logger.LogInformation(
                    "ZombieGametypeRecovered: live gametype is zombie again on {Server} (gametype={Gametype})",
                    server.ServerName, server.Gametype);
            }

            return true;
        }

        bool known, firstFallback;
        lock (_zombieServerStateLock)
        {
            known = _knownZombieServerIds.Contains(serverId);
            firstFallback = known && _gametypeFallbackActive.Add(serverId);
        }

        if (!known)
        {
            return false;
        }

        if (firstFallback)
        {
            _logger.LogWarning(
                "ZombieGametypeFallback: live IsZombieServer() returned false on known zombie server {Server} " +
                "(game={Game}, gametype={Gametype}); continuing zombie processing from session cache",
                server.ServerName, server.GameCode, server.Gametype);
        }

        return true;
    }

    private async Task OnClientDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        if (!ShouldProcessZombieEvent(clientEvent.Client.CurrentServer))
        {
            return;
        }

        if (_enhancer is not null)
        {
            await _enhancer.OnClientDisposed(clientEvent.Client, clientEvent.Client.CurrentServer);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnClientDataUpdated(ClientDataUpdateEvent updateEvent, CancellationToken token)
    {
        if (_enhancer is null || !ShouldProcessZombieEvent(updateEvent.Server))
        {
            return;
        }

        await _enhancer.ReconcileConnectedClients(updateEvent.Server);
    }

    private async Task OnClientAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        var server = clientEvent.Client.CurrentServer;
        if (!ShouldProcessZombieEvent(server))
        {
            // DIAGNOSTIC (zombie skill-leak phase 1): the server is a CoD
            // zombie-capable game (T4/T5/T6/T7) but IsZombieServer returned false,
            // so gametype was likely stale at auth time — SkillFunction will
            // never attach for this session. One-shot per (client, server).
            if (server?.GameCode is not (Reference.Game.T4 or Reference.Game.T5 or Reference.Game.T6 or Reference.Game.T7)) return;

            var raceFlag = $"ZmLog_AuthRace_{server.LegacyDatabaseId}";
            if (clientEvent.Client.GetAdditionalProperty<bool>(raceFlag)) return;
            clientEvent.Client.SetAdditionalProperty(raceFlag, true);
            _logger.LogWarning(
                "ZombieAuthRace: client={Name}({ClientId}) server={Server} game={Game} gametype={Gametype}",
                clientEvent.Client.Name, clientEvent.Client.ClientId, server.ServerName,
                server.GameCode, server.Gametype);
            return;
        }

        var skillFunc = _enhancer?.GetSkillCalculation() ?? ((_, stats) => stats.Skill);
        clientEvent.Client.SetAdditionalProperty("SkillFunction", skillFunc);
        clientEvent.Client.SetAdditionalProperty("EloRatingFunction", (EFClient _, EFClientStatistics _) => 1.0);

        // DIAGNOSTIC (zombie skill-leak phase 1): confirm SkillFunction was
        // attached for this client/server. One-shot per (client, server).
        var attachFlag = $"ZmLog_Attached_{server.LegacyDatabaseId}";
        if (!clientEvent.Client.GetAdditionalProperty<bool>(attachFlag))
        {
            clientEvent.Client.SetAdditionalProperty(attachFlag, true);
            _logger.LogWarning(
                "ZombieSkillFunctionAttached: client={Name}({ClientId}) server={Server}",
                clientEvent.Client.Name, clientEvent.Client.ClientId, server.ServerName);
        }

        if (_enhancer is not null)
        {
            await _enhancer.OnClientAuthorized(clientEvent.Client, clientEvent.Client.CurrentServer);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnScriptEvent(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (!ShouldProcessZombieEvent(scriptEvent.Server))
        {
            return;
        }

        var parsedScriptEvent = _zombieEventParser.ParseScriptEvent(scriptEvent);

        if (parsedScriptEvent is null)
        {
            return;
        }

        parsedScriptEvent.Owner = scriptEvent.Owner;

        // Track current round number on the server for webfront display
        scriptEvent.Owner.ZombieRoundNumber = parsedScriptEvent switch
        {
            PlayerRoundDataGameEvent roundData => roundData.CurrentRound,
            RoundEndEvent roundEnd => roundEnd.RoundNumber,
            _ => scriptEvent.Owner.ZombieRoundNumber
        };

        // Bridge zombie kills/damage/deaths to the standard Stats plugin (K/D/Score/hit locations)
        ConvertToStatsEvent(scriptEvent, parsedScriptEvent);

        // Forward to premium for full zombie-specific processing
        if (_enhancer is not null)
        {
            _enhancer.ProcessEvent(parsedScriptEvent);
            await _enhancer.UpdateState(token);
        }
    }

    private static void ConvertToStatsEvent(GameScriptEvent scriptEvent, GameEventV2 parsedScriptEvent)
    {
        var zombieClient = new EFClient
        {
            CurrentServer = scriptEvent.Owner,
            CurrentAlias = new EFAlias
            {
                Name = "Zombie"
            }
        };
        zombieClient.SetAdditionalProperty("ClientStats", new EFClientStatistics());

        switch (parsedScriptEvent)
        {
            case ZombieKilledGameEvent zombieKilledGameEvent:
                var zkAttacker = scriptEvent.Server.ConnectedClients.FirstOrDefault(client =>
                    client.NetworkId == zombieKilledGameEvent.Attacker.NetworkId);
                if (zkAttacker == null) break;
                scriptEvent.Owner.Manager.QueueEvent(new ClientKillEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Origin = zkAttacker,
                    Target = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = zombieKilledGameEvent.Owner
                });
                break;
            case ZombieDamageGameEvent zombieDamageGameEvent:
                var zdAttacker = scriptEvent.Server.ConnectedClients.FirstOrDefault(client =>
                    client.NetworkId == zombieDamageGameEvent.Attacker.NetworkId);
                if (zdAttacker == null) break;
                scriptEvent.Owner.Manager.QueueEvent(new ClientDamageEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Origin = zdAttacker,
                    Target = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = zombieDamageGameEvent.Owner
                });
                break;
            case PlayerKilledGameEvent playerKilledGameEvent:
                var pkTarget = scriptEvent.Server.ConnectedClients.FirstOrDefault(client =>
                    client.NetworkId == playerKilledGameEvent.Target.NetworkId);
                if (pkTarget == null) break;
                scriptEvent.Owner.Manager.QueueEvent(new ClientKillEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Target = pkTarget,
                    Origin = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = playerKilledGameEvent.Owner
                });
                break;
        }
    }

    private async Task OnMatchEnded(MatchEndEvent matchEvent, CancellationToken token)
    {
        if (!ShouldProcessZombieEvent(matchEvent.Server))
        {
            return;
        }

        matchEvent.Owner.ZombieRoundNumber = null;

        if (_enhancer is not null)
        {
            await _enhancer.OnMatchEnded(matchEvent.Server);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnMatchStarted(MatchStartEvent matchEvent, CancellationToken token)
    {
        if (!matchEvent.Server.IsZombieServer())
        {
            return;
        }

        // Reset round display BEFORE the ConnectedClients guard. On T7x the
        // game log doesn't emit ExitLevel/ShutdownGame between matches, so
        // OnMatchEnded never fires; this is the only opportunity to clear
        // the stale ZombieRoundNumber from the previous match. The J event
        // also fires same-second as InitGame so ConnectedClients is racey
        // here — clearing before the guard makes the reset deterministic.
        matchEvent.Owner.ZombieRoundNumber = null;

        if (!matchEvent.Server.ConnectedClients.Any())
        {
            return;
        }

        lock (_zombieServerStateLock)
        {
            _knownZombieServerIds.Add(matchEvent.Server.LegacyDatabaseId);
        }

        if (_enhancer is not null)
        {
            _enhancer.OnMatchStarted(matchEvent.Server);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnUnload(IManager manager, CancellationToken token)
    {
        // Flush any queued zombie persistence so in-flight match/round/event rows
        // don't leave the DB in an inconsistent state on restart.
        if (_enhancer is not null)
        {
            try
            {
                await _enhancer.UpdateState(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush zombie stats on unload");
            }
        }
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _logger.LogInformation("{Plugin} by {Author} v{Version} loading...", Name, Author, Version);

        if (_enhancer is not null)
        {
            await _enhancer.Initialize();
            manager.CustomStatsMetrics.Add(_enhancer.GetTopStatsMetrics);
            // Per-player Advanced Stats metric injection retired — the dedicated career
            // page owns those numbers now; the stats page shows the premium plugin's slim
            // summary card (client-advanced-stats slot) instead.
            manager.CustomTopStatsTransformers.Add(_enhancer.TransformTopStats);
            // The /stats/zombies nav page is registered by the ZombieStatsPremium plugin, which now
            // owns the zombie web pages (shipped as a web bundle).
        }
        else
        {
            manager.CustomStatsMetrics.Add(GetPremiumUpsellMetrics);
        }
    }

    private async Task GetPremiumUpsellMetrics(Dictionary<int, List<EFMeta>> meta, long? serverId,
        string performanceBucketCode, bool isTopStats)
    {
        if (isTopStats || meta.Count == 0 || serverId is null)
        {
            return;
        }

        // Check in-memory first (servers we've seen zombie events from this session)
        bool isZombieServer;
        lock (_zombieServerStateLock)
        {
            isZombieServer = _knownZombieServerIds.Contains(serverId.Value);
        }

        // Fall back to DB check (zombie data may exist from a previous session with premium)
        if (!isZombieServer)
        {
            await using var context = _contextFactory.CreateContext(false);
            isZombieServer = await context.ZombieClientStatAggregates
                .AnyAsync(stat => stat.ServerId == serverId);
        }

        if (!isZombieServer)
        {
            return;
        }

        meta.First().Value.Add(new EFMeta
        {
            Key = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_ZOMBIE_STATS_PREMIUM_UPSELL_KEY"],
            Value = Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_ZOMBIE_STATS_PREMIUM_UPSELL_VALUE"]
        });
    }
}
