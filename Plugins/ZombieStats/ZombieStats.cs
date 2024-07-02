using Data.Models;
using Data.Models.Client.Stats;
using Data.Models.Zombie;
using Humanizer;
using IW4MAdmin.Plugins.ZombieStats.Events;
using IW4MAdmin.Plugins.ZombieStats.States;
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

public class ZombieStats : IPluginV2
{
    private readonly ILogger<ZombieStats> _logger;
    private readonly ZombieEventParser _zombieEventParser;
    private readonly ZombieEventProcessor _zombieEventProcessor;
    private readonly ZombieClientStateManager _stateManager;
    public string Name { get; } = nameof(ZombieStats).Titleize();
    public string Author => "RaidMax";
    public string Version => "2023.4-alpha";

    public ZombieStats(ILogger<ZombieStats> logger, ZombieEventParser zombieEventParser,
        ZombieEventProcessor zombieEventProcessor, ZombieClientStateManager stateManager)
    {
        _logger = logger;
        _zombieEventParser = zombieEventParser;
        _zombieEventProcessor = zombieEventProcessor;
        _stateManager = stateManager;

        IManagementEventSubscriptions.Load += OnLoad;
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientAuthorized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientDisposed;
        IGameEventSubscriptions.ScriptEventTriggered += OnScriptEvent;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IGameEventSubscriptions.MatchStarted += OnMatchStarted;
    }
    
    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ZombieEventParser>()
            .AddSingleton<ZombieEventProcessor>()
            .AddSingleton<ZombieClientStateManager>();
    }

    private async Task OnClientDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        if (!clientEvent.Client.CurrentServer.IsZombieServer())
        {
            return;
        }
        
        _stateManager.TrackEventForLog(clientEvent.Client.CurrentServer, EventLogType.LeftMatch, clientEvent.Client);
        _zombieEventProcessor.ProcessEvent(new PlayerRoundDataGameEvent
        {
            Origin = clientEvent.Client,
            Owner = clientEvent.Client.CurrentServer
        });
        _stateManager.UntrackClient(clientEvent.Client, clientEvent.Client.CurrentServer);
        await _stateManager.UpdateState(token);
    }

    private async Task OnClientAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        if (!clientEvent.Client.CurrentServer.IsZombieServer())
        {
            return;
        }
        
        clientEvent.Client.SetAdditionalProperty("SkillFunction", _zombieEventProcessor.SkillCalculation());
        clientEvent.Client.SetAdditionalProperty("EloRatingFunction", (EFClient _, EFClientStatistics _) => 1.0);
        await _stateManager.TrackClient(clientEvent.Client, clientEvent.Client.CurrentServer);
        _stateManager.TrackEventForLog(clientEvent.Client.CurrentServer, EventLogType.JoinedMatch, clientEvent.Client);
        await _stateManager.UpdateState(token);
    }

    private async Task OnScriptEvent(GameScriptEvent scriptEvent, CancellationToken token)
    {
        if (!scriptEvent.Server.IsZombieServer())
        {
            return;
        }
        
        var parsedScriptEvent = _zombieEventParser.ParseScriptEvent(scriptEvent);

        if (parsedScriptEvent is null)
        {
            return;
        }

        parsedScriptEvent.Owner = scriptEvent.Owner;
        _zombieEventProcessor.ProcessEvent(parsedScriptEvent);
        await _stateManager.UpdateState(token);

        ConvertToStatsEvent(scriptEvent, parsedScriptEvent);
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
                scriptEvent.Owner.Manager.QueueEvent(new ClientKillEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Origin = scriptEvent.Server.ConnectedClients.First(client =>
                        client.NetworkId == zombieKilledGameEvent.Attacker.NetworkId),
                    Target = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = zombieKilledGameEvent.Owner
                });
                break;
            case ZombieDamageGameEvent zombieDamageGameEvent:
                scriptEvent.Owner.Manager.QueueEvent(new ClientDamageEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Origin = scriptEvent.Server.ConnectedClients.First(client =>
                        client.NetworkId == zombieDamageGameEvent.Attacker.NetworkId),
                    Target = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = zombieDamageGameEvent.Owner
                });
                break;
            case PlayerKilledGameEvent playerKilledGameEvent:
                scriptEvent.Owner.Manager.QueueEvent(new ClientKillEvent
                {
                    Type = GameEvent.EventType.Kill,
                    Data = string.Join(';', scriptEvent.ScriptData.Split(';')[1..]).TrimStart('A'),
                    Target = scriptEvent.Server.ConnectedClients.First(client =>
                        client.NetworkId == playerKilledGameEvent.Target.NetworkId),
                    Origin = zombieClient,
                    GameTime = scriptEvent.GameTime,
                    Source = GameEvent.EventSource.Log,
                    Owner = playerKilledGameEvent.Owner
                });
                break;
            case RoundEndEvent roundEndEvent:
                scriptEvent.Owner.Manager.QueueEvent(roundEndEvent);
                break;
        }
    }

    private async Task OnMatchEnded(MatchEndEvent matchEvent, CancellationToken token)
    {
        if (!matchEvent.Server.IsZombieServer())
        {
            return;
        }
        
        _stateManager.TrackEventForLog(matchEvent.Server, EventLogType.MatchEnded);
        _stateManager.EndMatch(matchEvent.Server);
        await _stateManager.UpdateState(token);
    }

    private async Task OnMatchStarted(MatchStartEvent matchEvent, CancellationToken token)
    {
        if (!matchEvent.Server.ConnectedClients.Any() || !matchEvent.Server.IsZombieServer())
        {
            return;
        }

        _stateManager.CreateMatch(matchEvent.Server);
        _stateManager.TrackEventForLog(matchEvent.Server, EventLogType.MatchStarted);
        await _stateManager.UpdateState(token);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _logger.LogInformation("{Plugin} by {Author} v{Version} loading...", Name, Author, Version);
        
        manager.CustomStatsMetrics.Add(_stateManager.GetTopStatsMetrics);
        manager.CustomStatsMetrics.Add(_stateManager.GetAdvancedStatsMetrics);
        await _stateManager.Initialize();
    }
}
