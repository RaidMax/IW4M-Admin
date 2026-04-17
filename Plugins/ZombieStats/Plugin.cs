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
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEvent;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IGameEventSubscriptions.MatchStarted += OnMatchStarted;
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ZombieEventParser>();
    }

    private async Task OnClientDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        if (!clientEvent.Client.CurrentServer.IsZombieServer())
        {
            return;
        }

        if (_enhancer is not null)
        {
            await _enhancer.OnClientDisposed(clientEvent.Client, clientEvent.Client.CurrentServer);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnClientAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        if (!clientEvent.Client.CurrentServer.IsZombieServer())
        {
            return;
        }

        var skillFunc = _enhancer?.GetSkillCalculation() ?? ((_, stats) => stats.Skill);
        clientEvent.Client.SetAdditionalProperty("SkillFunction", skillFunc);
        clientEvent.Client.SetAdditionalProperty("EloRatingFunction", (EFClient _, EFClientStatistics _) => 1.0);

        if (_enhancer is not null)
        {
            await _enhancer.OnClientAuthorized(clientEvent.Client, clientEvent.Client.CurrentServer);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnScriptEvent(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (!scriptEvent.Server.IsZombieServer())
        {
            return;
        }

        _knownZombieServerIds.Add(scriptEvent.Server.LegacyDatabaseId);

        var parsedScriptEvent = _zombieEventParser.ParseScriptEvent(scriptEvent);

        if (parsedScriptEvent is null)
        {
            return;
        }

        parsedScriptEvent.Owner = scriptEvent.Owner;

        // Track current round number on the server for webfront display
        switch (parsedScriptEvent)
        {
            case PlayerRoundDataGameEvent roundData:
                scriptEvent.Owner.ZombieRoundNumber = roundData.CurrentRound;
                break;
            case RoundEndEvent roundEnd:
                scriptEvent.Owner.ZombieRoundNumber = roundEnd.RoundNumber;
                break;
        }

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
        if (!matchEvent.Server.IsZombieServer())
        {
            return;
        }

        matchEvent.Owner.ZombieRoundNumber = null;

        if (_enhancer is not null)
        {
            _enhancer.OnMatchEnded(matchEvent.Server);
            await _enhancer.UpdateState(token);
        }
    }

    private async Task OnMatchStarted(MatchStartEvent matchEvent, CancellationToken token)
    {
        if (!matchEvent.Server.ConnectedClients.Any() || !matchEvent.Server.IsZombieServer())
        {
            return;
        }

        _knownZombieServerIds.Add(matchEvent.Server.LegacyDatabaseId);
        matchEvent.Owner.ZombieRoundNumber = null;

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
            manager.CustomStatsMetrics.Add(_enhancer.GetAdvancedStatsMetrics);
            manager.GetPageList().Pages.Add("Zombie Leaderboard", "/stats/zombies");
        }
        else
        {
            manager.CustomStatsMetrics.Add(GetPremiumUpsellMetrics);
        }
    }

    private async Task GetPremiumUpsellMetrics(Dictionary<int, List<EFMeta>> meta, long? serverId,
        string performanceBucketCode, bool isTopStats)
    {
        if (isTopStats || !meta.Any() || serverId is null)
        {
            return;
        }

        // Check in-memory first (servers we've seen zombie events from this session)
        var isZombieServer = _knownZombieServerIds.Contains(serverId.Value);

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
            Key = "Advanced Zombie Stats",
            Value = "Available with Zombie Stats Premium"
        });
    }
}
